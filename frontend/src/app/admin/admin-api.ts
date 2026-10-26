import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface TemplateListItem {
  id: number;
  slug: string;
  name: string;
  priceCents: number;
  position: number;
  isPublished: boolean;
  mainPreviewUrl: string | null;
  previewCount: number;
  hasXlsx: boolean;
  hasGoogleSheets: boolean;
}

export interface PreviewVariant {
  width: number;
  height: number;
  url: string;
}

export interface Preview {
  id: number;
  position: number;
  variants: PreviewVariant[];
}

export interface TemplateDetail {
  id: number;
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  position: number;
  isPublished: boolean;
  slugLocked: boolean;
  hasXlsx: boolean;
  googleSheetsCopyUrl: string | null;
  previews: Preview[];
  bundles: { id: number; name: string; isPublished: boolean }[];
  missingForPublication: string[];
}

export interface TemplateInput {
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  position: number | null;
  googleSheetsCopyUrl: string | null;
}

export interface BundleListItem {
  id: number;
  slug: string;
  name: string;
  priceCents: number;
  position: number;
  isPublished: boolean;
  templateCount: number;
  savingsCents: number;
}

export interface BundleTemplate {
  id: number;
  slug: string;
  name: string;
  priceCents: number;
  isPublished: boolean;
}

export interface BundleDetail {
  id: number;
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  position: number;
  isPublished: boolean;
  slugLocked: boolean;
  templates: BundleTemplate[];
  templatesTotalCents: number;
  savingsCents: number;
  publicationProblems: string[];
}

export interface BundleInput {
  slug: string;
  name: string;
  description: string;
  priceCents: number;
  position: number | null;
}

export interface OrderListItem {
  id: number;
  email: string;
  productType: 'plantilla' | 'paquete';
  productName: string;
  pricePaidCents: number;
  paidAt: string;
  status: 'Paid' | 'Refunded';
}

export const ADMIN_API = '/api/admin';
const TEMPLATES = `${ADMIN_API}/plantillas`;
const BUNDLES = `${ADMIN_API}/paquetes`;

/** Las operaciones que modifican datos. Las listas se leen con `httpResource` en cada pantalla. */
@Service()
export class AdminApi {
  private readonly http = inject(HttpClient);

  getTemplate(id: number) {
    return firstValueFrom(this.http.get<TemplateDetail>(`${TEMPLATES}/${id}`));
  }

  createTemplate(input: TemplateInput) {
    return firstValueFrom(this.http.post<TemplateDetail>(TEMPLATES, input));
  }

  updateTemplate(id: number, input: TemplateInput) {
    return firstValueFrom(this.http.put<TemplateDetail>(`${TEMPLATES}/${id}`, input));
  }

  deleteTemplate(id: number) {
    return firstValueFrom(this.http.delete<void>(`${TEMPLATES}/${id}`));
  }

  setTemplatePublished(id: number, published: boolean) {
    const action = published ? 'publicar' : 'despublicar';
    return firstValueFrom(this.http.post<TemplateDetail>(`${TEMPLATES}/${id}/${action}`, null));
  }

  uploadPreview(id: number, file: File) {
    return firstValueFrom(
      this.http.post<TemplateDetail>(`${TEMPLATES}/${id}/previews`, formData(file)),
    );
  }

  reorderPreviews(id: number, previewIds: number[]) {
    return firstValueFrom(
      this.http.put<TemplateDetail>(`${TEMPLATES}/${id}/previews/orden`, { previewIds }),
    );
  }

  deletePreview(id: number, previewId: number) {
    return firstValueFrom(
      this.http.delete<TemplateDetail>(`${TEMPLATES}/${id}/previews/${previewId}`),
    );
  }

  uploadXlsx(id: number, file: File) {
    return firstValueFrom(this.http.put<TemplateDetail>(`${TEMPLATES}/${id}/xlsx`, formData(file)));
  }

  getBundle(id: number) {
    return firstValueFrom(this.http.get<BundleDetail>(`${BUNDLES}/${id}`));
  }

  createBundle(input: BundleInput) {
    return firstValueFrom(this.http.post<BundleDetail>(BUNDLES, input));
  }

  updateBundle(id: number, input: BundleInput) {
    return firstValueFrom(this.http.put<BundleDetail>(`${BUNDLES}/${id}`, input));
  }

  deleteBundle(id: number) {
    return firstValueFrom(this.http.delete<void>(`${BUNDLES}/${id}`));
  }

  setBundlePublished(id: number, published: boolean) {
    const action = published ? 'publicar' : 'despublicar';
    return firstValueFrom(this.http.post<BundleDetail>(`${BUNDLES}/${id}/${action}`, null));
  }

  addTemplateToBundle(id: number, templateId: number) {
    return firstValueFrom(
      this.http.post<BundleDetail>(`${BUNDLES}/${id}/plantillas`, { templateId }),
    );
  }

  removeTemplateFromBundle(id: number, templateId: number) {
    return firstValueFrom(
      this.http.delete<BundleDetail>(`${BUNDLES}/${id}/plantillas/${templateId}`),
    );
  }
}

function formData(file: File): FormData {
  const data = new FormData();
  data.append('file', file);
  return data;
}
