import test from "node:test";
import assert from "node:assert/strict";
import { parseKiwifyPayload } from "../src/index.js";

test("extrai uma compra aprovada de objetos aninhados", () => {
  const result = parseKiwifyPayload({
    event: "Compra aprovada",
    order: {
      order_id: "order-123",
      order_ref: "ABC123"
    },
    Customer: {
      full_name: "Cliente Teste",
      email: "CLIENTE@EXEMPLO.COM"
    },
    Product: {
      product_id: "product-1"
    }
  });

  assert.deepEqual(result, {
    event: "compra_aprovada",
    orderId: "order-123",
    orderRef: "ABC123",
    email: "cliente@exemplo.com",
    customerName: "Cliente Teste",
    productId: "product-1"
  });
});

test("normaliza evento de reembolso", () => {
  const result = parseKiwifyPayload({
    event_type: "Compra reembolsada",
    transaction_id: "tx-10",
    buyer_email: "buyer@example.com"
  });

  assert.equal(result.event, "compra_reembolsada");
  assert.equal(result.orderId, "tx-10");
  assert.equal(result.orderRef, "tx-10");
});
