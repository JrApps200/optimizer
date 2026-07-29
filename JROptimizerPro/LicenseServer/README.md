# Servidor de licenças — JR Optimizer Pro

API de licenças para Cloudflare Workers + D1. O aplicativo exige:

- e-mail usado na compra;
- código do pedido da Kiwify;
- um único computador por compra.

Eventos de compra aprovada ativam a licença. Reembolso, chargeback e
cancelamento revogam o token.

## Publicação

1. Instale as dependências com `npm install`.
2. Entre na Cloudflare com `npx wrangler login`.
3. Crie o banco: `npx wrangler d1 create jr-optimizer-licenses`.
4. Copie o `database_id` retornado para `wrangler.jsonc`.
5. Cadastre os segredos:
   - `npx wrangler secret put KIWIFY_WEBHOOK_SECRET`
   - `npx wrangler secret put ADMIN_TOKEN`
6. Execute `npm run db:migrate:remote`.
7. Execute `npm run deploy`.
8. Copie a URL publicada para `Core/LicenseOptions.cs`.

## Kiwify

No painel da Kiwify, abra **Apps > Webhooks** e crie um webhook para o produto.
Use:

`https://SEU-WORKER.workers.dev/webhooks/kiwify/SEU_SEGREDO`

Marque compra aprovada, reembolso e chargeback. Para assinatura, marque também
renovação e cancelamento. Teste o webhook pelo próprio painel antes de vender.

O segredo no final da URL deve ser o mesmo valor salvo em
`KIWIFY_WEBHOOK_SECRET`. Nunca inclua esse valor no aplicativo.
