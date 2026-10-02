/**
 * Business details printed at the top of every receipt. Override per deployment with
 * VITE_BUSINESS_* variables (see .env.example) - these defaults are placeholders, so
 * set the real address, phone and KRA PIN before printing customer-facing receipts.
 */
export const BUSINESS = {
  name: import.meta.env.VITE_BUSINESS_NAME ?? 'Edd Tech',
  tagline: import.meta.env.VITE_BUSINESS_TAGLINE ?? 'Electronics & Repairs',
  address: import.meta.env.VITE_BUSINESS_ADDRESS ?? 'Nairobi, Kenya',
  phone: import.meta.env.VITE_BUSINESS_PHONE ?? '',
  kraPin: import.meta.env.VITE_BUSINESS_KRA_PIN ?? '',
  footer: import.meta.env.VITE_RECEIPT_FOOTER ?? 'Thank you for shopping with us!',
}