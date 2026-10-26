export type ProductType = 'plantilla' | 'paquete';

export interface CheckoutCreated {
  url: string;
}

/** `token` solo llega en la primera respuesta `paid`: después, el cliente usa el enlace del email. */
export interface CheckoutStatus {
  status: 'pending' | 'paid';
  token: string | null;
}

export interface Purchase {
  productType: ProductType;
  productName: string;
  email: string;
  pricePaidCents: number;
  paidAt: string;
  isRefunded: boolean;
  templates: PurchaseTemplate[];
}

/** `googleSheetsCopyUrl` y `downloadUrl` son nulos si la Compra está reembolsada. */
export interface PurchaseTemplate {
  id: number;
  slug: string;
  name: string;
  googleSheetsCopyUrl: string | null;
  downloadUrl: string | null;
}
