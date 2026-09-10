# Meta WhatsApp Cloud API rollout

The POS sends only transactional messages, and only where the customer has explicitly opted in. A receipt confirmation is sent after a completed sale. M-Pesa confirmation is sent after the successful Daraja callback. Repair status notifications are sent after a repair status change. A WhatsApp delivery error is logged and never reverses the completed sale, payment, or repair update.

## One-time Meta setup

1. Create a Meta Business Portfolio and a Meta app with the **WhatsApp** product. Add and verify the shop's sending number. In development, add each test recipient in Meta's test-number recipient list.
2. Create a system user in Business Settings, assign the WhatsApp app and WhatsApp Business Account, then generate a long-lived system-user access token with `whatsapp_business_messaging`. Do not use the short-lived token from the API setup screen in production.
3. In WhatsApp Manager, create the three templates below in the **Utility** category. Keep the names, language, and exactly two body variables aligned with the configuration.
4. Store credentials outside source control. For local development, run from `server`:

```powershell
dotnet user-secrets set "WhatsApp:PhoneNumberId" "YOUR_PHONE_NUMBER_ID" --project Pos.API
dotnet user-secrets set "WhatsApp:AccessToken" "YOUR_SYSTEM_USER_TOKEN" --project Pos.API
```

In production set `WhatsApp__PhoneNumberId` and `WhatsApp__AccessToken` in the host's secret store. Set `WhatsApp__ApiVersion` to a Graph API version currently supported by Meta before deploying.

5. Apply the migration:

```powershell
dotnet ef database update --project Pos.Infrastructure --startup-project Pos.API
```

## Templates to submit

All three are utility templates: they report a transaction or service event, use no marketing language, and do not include an upsell, coupon, or call to purchase. Supply the shown example values in Meta's variable sample fields.

| Template name | Category | Body | Example values |
|---|---|---|---|
| `receipt_delivery` | Utility | `Thank you for your purchase. Receipt {{1}}; total paid: KES {{2}}.` | `AB12CD34`, `1,250.00` |
| `mpesa_payment_confirmation` | Utility | `We have received your M-Pesa payment of KES {{1}}. Reference: {{2}}.` | `1,250.00`, `TJK4A8M9Q2` |
| `repair_status_update` | Utility | `Your repair ticket {{1}} is now {{2}}.` | `RPR-20260909-001`, `Ready` |

Use language `en` (or update `WhatsApp:TemplateLanguage` to the language code selected in WhatsApp Manager). Avoid emoji, URL shorteners, promotional wording, and variables that could contain unbounded/free-form text. Meta reviews each submission individually, so approval cannot be guaranteed, but these are narrowly framed transaction/service notifications.

## Operating rules

- Ask for consent at the counter using the new customer checkbox and explain that it covers receipts, payment confirmations, and repair updates. Staff can withdraw consent from the customer record at any time.
- Send the customer number in international E.164 form, for example `254712345678`.
- The receipt message currently provides a receipt reference and total, not a PDF attachment. Adding a PDF later should use Meta's document message flow and a protected, time-limited download URL; never expose an unauthenticated permanent receipt URL.
- Watch Meta delivery/webhook events before relying on delivery status. The API response only confirms that Meta accepted the request, not that the customer read it.
