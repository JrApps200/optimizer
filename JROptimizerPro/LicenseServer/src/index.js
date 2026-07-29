const JSON_HEADERS = {
  "content-type": "application/json; charset=utf-8",
  "cache-control": "no-store"
};

const APPROVAL_EVENTS = new Set([
  "compra_aprovada",
  "purchase_approved",
  "order_approved",
  "paid",
  "approved",
  "subscription_renewed",
  "assinatura_renovada"
]);

const REVOCATION_EVENTS = new Set([
  "reembolso",
  "compra_reembolsada",
  "purchase_refunded",
  "refund",
  "refunded",
  "chargeback",
  "assinatura_cancelada",
  "subscription_canceled",
  "subscription_cancelled"
]);

export default {
  async fetch(request, env) {
    try {
      const url = new URL(request.url);

      if (request.method === "GET" && url.pathname === "/health")
        return json({ ok: true, service: "jr-optimizer-license" });

      if (request.method === "POST" && url.pathname === "/v1/licenses/activate")
        return activateLicense(request, env);

      if (request.method === "POST" && url.pathname === "/v1/licenses/validate")
        return validateLicense(request, env);

      if (request.method === "POST" && url.pathname.startsWith("/webhooks/kiwify/"))
        return receiveKiwifyWebhook(request, env, url.pathname.split("/").pop());

      if (request.method === "POST" && url.pathname === "/admin/licenses")
        return createManualLicense(request, env);

      return json({ valid: false, message: "Endpoint não encontrado." }, 404);
    } catch (error) {
      console.error("request_failed", error);
      return json({ valid: false, message: "Erro interno do servidor." }, 500);
    }
  }
};

async function activateLicense(request, env) {
  const body = await readJson(request);
  const email = normalizeEmail(body.email);
  const purchaseCode = clean(body.purchaseCode);
  const machineId = clean(body.machineId);

  if (!email || !purchaseCode || !isMachineId(machineId))
    return json({ valid: false, message: "Dados de ativação incompletos." }, 400);

  const license = await env.DB.prepare(`
    SELECT id, email, customer_name, status, machine_id
    FROM licenses
    WHERE email = ?1 AND (order_id = ?2 OR order_ref = ?2)
    LIMIT 1
  `).bind(email, purchaseCode).first();

  if (!license)
    return json({ valid: false, message: "Compra não localizada. Confira o e-mail e o código do pedido." }, 404);

  if (license.status !== "active")
    return json({ valid: false, message: "Esta licença foi cancelada ou reembolsada." }, 403);

  if (license.machine_id && license.machine_id !== machineId)
    return json({ valid: false, message: "Esta licença já está vinculada a outro computador." }, 409);

  const now = new Date().toISOString();
  const token = randomToken();
  const tokenHash = await sha256(token);

  await env.DB.batch([
    env.DB.prepare(`
      UPDATE licenses
      SET machine_id = ?1, activated_at = COALESCE(activated_at, ?2), updated_at = ?2
      WHERE id = ?3
    `).bind(machineId, now, license.id),
    env.DB.prepare(`
      UPDATE license_tokens SET revoked_at = ?1
      WHERE license_id = ?2 AND revoked_at IS NULL
    `).bind(now, license.id),
    env.DB.prepare(`
      INSERT INTO license_tokens
      (token_hash, license_id, machine_id, created_at, last_seen_at)
      VALUES (?1, ?2, ?3, ?4, ?4)
    `).bind(tokenHash, license.id, machineId, now)
  ]);

  return json({
    valid: true,
    token,
    customerName: license.customer_name,
    status: "active",
    message: "Licença ativada com sucesso."
  });
}

async function validateLicense(request, env) {
  const body = await readJson(request);
  const token = clean(body.token);
  const machineId = clean(body.machineId);

  if (!token || !isMachineId(machineId))
    return json({ valid: false, message: "Token de licença inválido." }, 400);

  const tokenHash = await sha256(token);
  const record = await env.DB.prepare(`
    SELECT t.license_id, t.machine_id, t.revoked_at, l.status, l.customer_name
    FROM license_tokens t
    JOIN licenses l ON l.id = t.license_id
    WHERE t.token_hash = ?1
    LIMIT 1
  `).bind(tokenHash).first();

  if (!record || record.revoked_at || record.status !== "active")
    return json({ valid: false, status: "revoked", message: "Licença inválida ou revogada." }, 403);

  if (record.machine_id !== machineId)
    return json({ valid: false, status: "device_mismatch", message: "Licença vinculada a outro computador." }, 403);

  await env.DB.prepare(`
    UPDATE license_tokens SET last_seen_at = ?1 WHERE token_hash = ?2
  `).bind(new Date().toISOString(), tokenHash).run();

  return json({
    valid: true,
    customerName: record.customer_name,
    status: "active",
    message: "Licença válida."
  });
}

async function receiveKiwifyWebhook(request, env, pathSecret) {
  if (!env.KIWIFY_WEBHOOK_SECRET || !constantTimeEqual(pathSecret, env.KIWIFY_WEBHOOK_SECRET))
    return json({ ok: false }, 401);

  const payload = await readJson(request);
  const sale = parseKiwifyPayload(payload);

  if (!sale.event || !sale.orderId || !sale.email)
    return json({ ok: false, message: "Payload sem evento, pedido ou comprador." }, 400);

  if (env.KIWIFY_PRODUCT_ID && sale.productId !== env.KIWIFY_PRODUCT_ID)
    return json({ ok: true, ignored: "product" });

  const eventKey = await sha256(`${sale.event}|${sale.orderId}|${JSON.stringify(payload)}`);
  const existing = await env.DB.prepare(
    "SELECT event_key FROM webhook_events WHERE event_key = ?1"
  ).bind(eventKey).first();
  if (existing)
    return json({ ok: true, duplicate: true });

  const now = new Date().toISOString();
  if (APPROVAL_EVENTS.has(sale.event)) {
    await env.DB.batch([
      env.DB.prepare(`
        INSERT INTO licenses
          (id, email, customer_name, order_id, order_ref, product_id, status, created_at, updated_at)
        VALUES (?1, ?2, ?3, ?4, ?5, ?6, 'active', ?7, ?7)
        ON CONFLICT(order_id) DO UPDATE SET
          email = excluded.email,
          customer_name = excluded.customer_name,
          order_ref = excluded.order_ref,
          product_id = excluded.product_id,
          status = 'active',
          updated_at = excluded.updated_at
      `).bind(crypto.randomUUID(), sale.email, sale.customerName, sale.orderId, sale.orderRef, sale.productId, now),
      env.DB.prepare(`
        INSERT INTO webhook_events (event_key, event_name, received_at)
        VALUES (?1, ?2, ?3)
      `).bind(eventKey, sale.event, now)
    ]);
  } else if (REVOCATION_EVENTS.has(sale.event)) {
    await env.DB.batch([
      env.DB.prepare(`
        UPDATE licenses SET status = 'revoked', updated_at = ?1
        WHERE order_id = ?2 OR order_ref = ?3
      `).bind(now, sale.orderId, sale.orderRef),
      env.DB.prepare(`
        UPDATE license_tokens SET revoked_at = ?1
        WHERE license_id IN (
          SELECT id FROM licenses WHERE order_id = ?2 OR order_ref = ?3
        ) AND revoked_at IS NULL
      `).bind(now, sale.orderId, sale.orderRef),
      env.DB.prepare(`
        INSERT INTO webhook_events (event_key, event_name, received_at)
        VALUES (?1, ?2, ?3)
      `).bind(eventKey, sale.event, now)
    ]);
  } else {
    await env.DB.prepare(`
      INSERT INTO webhook_events (event_key, event_name, received_at)
      VALUES (?1, ?2, ?3)
    `).bind(eventKey, sale.event, now).run();
  }

  return json({ ok: true });
}

async function createManualLicense(request, env) {
  if (!env.ADMIN_TOKEN || request.headers.get("authorization") !== `Bearer ${env.ADMIN_TOKEN}`)
    return json({ ok: false }, 401);

  const body = await readJson(request);
  const email = normalizeEmail(body.email);
  const orderId = clean(body.orderId) || `manual-${crypto.randomUUID()}`;
  if (!email)
    return json({ ok: false, message: "E-mail inválido." }, 400);

  const now = new Date().toISOString();
  await env.DB.prepare(`
    INSERT INTO licenses
      (id, email, customer_name, order_id, order_ref, product_id, status, created_at, updated_at)
    VALUES (?1, ?2, ?3, ?4, ?5, 'manual', 'active', ?6, ?6)
    ON CONFLICT(order_id) DO UPDATE SET status = 'active', updated_at = excluded.updated_at
  `).bind(
    crypto.randomUUID(),
    email,
    clean(body.customerName),
    orderId,
    clean(body.purchaseCode) || orderId,
    now
  ).run();

  return json({ ok: true, email, purchaseCode: clean(body.purchaseCode) || orderId });
}

export function parseKiwifyPayload(payload) {
  const event = normalizeEvent(firstValue(payload, [
    "event", "event_type", "type", "order_status", "status"
  ]));
  const orderId = clean(firstValue(payload, [
    "order_id", "orderId", "sale_id", "transaction_id"
  ]));
  const orderRef = clean(firstValue(payload, [
    "order_ref", "order_reference", "reference", "checkout_id"
  ])) || orderId;
  const email = normalizeEmail(firstValue(payload, [
    "email", "customer_email", "buyer_email"
  ]));
  const customerName = clean(firstValue(payload, [
    "full_name", "customer_name", "buyer_name", "name"
  ]));
  const productId = clean(firstValue(payload, [
    "product_id", "productId", "offer_id"
  ]));

  return { event, orderId, orderRef, email, customerName, productId };
}

function firstValue(value, keys) {
  if (!value || typeof value !== "object")
    return "";

  for (const key of keys) {
    const direct = value[key];
    if (typeof direct === "string" || typeof direct === "number")
      return String(direct);
  }

  for (const child of Object.values(value)) {
    if (child && typeof child === "object") {
      const nested = firstValue(child, keys);
      if (nested)
        return nested;
    }
  }

  return "";
}

function normalizeEvent(value) {
  return clean(value)
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "_")
    .replace(/^_|_$/g, "");
}

function normalizeEmail(value) {
  const email = clean(value).toLowerCase();
  return email.includes("@") ? email : "";
}

function clean(value) {
  return typeof value === "string" ? value.trim() : "";
}

function isMachineId(value) {
  return /^[a-f0-9]{64}$/.test(value);
}

function randomToken() {
  const bytes = crypto.getRandomValues(new Uint8Array(24));
  const encoded = btoa(String.fromCharCode(...bytes))
    .replaceAll("+", "-")
    .replaceAll("/", "_")
    .replaceAll("=", "");
  return `jrop_${encoded}`;
}

async function sha256(value) {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest("SHA-256", bytes);
  return [...new Uint8Array(digest)]
    .map(byte => byte.toString(16).padStart(2, "0"))
    .join("");
}

function constantTimeEqual(left, right) {
  if (typeof left !== "string" || typeof right !== "string" || left.length !== right.length)
    return false;

  let difference = 0;
  for (let index = 0; index < left.length; index++)
    difference |= left.charCodeAt(index) ^ right.charCodeAt(index);
  return difference === 0;
}

async function readJson(request) {
  const type = request.headers.get("content-type") || "";
  if (!type.includes("application/json"))
    throw new Error("content_type_not_json");
  return request.json();
}

function json(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: JSON_HEADERS
  });
}
