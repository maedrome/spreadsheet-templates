export interface PreviewVariant {
  url: string;
  width: number;
  height: number;
}

export interface Preview {
  variants: PreviewVariant[];
}

export interface TemplateSummary {
  slug: string;
  name: string;
  priceCents: number;
  mainPreview: Preview | null;
}

export interface TemplateCard extends TemplateSummary {
  description: string;
}

export interface BundleCard {
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  savingsCents: number;
  templates: TemplateSummary[];
}

export interface Catalogo {
  bundles: BundleCard[];
  templates: TemplateCard[];
}

export interface TemplateDetail {
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  previews: Preview[];
}

export interface BundleDetail {
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  savingsCents: number;
  templates: TemplateSummary[];
}
