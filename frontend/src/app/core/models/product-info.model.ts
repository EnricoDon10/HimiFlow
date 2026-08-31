export interface LegalNotice {
  isConfigured: boolean;
  providerName: string | null;
  shortName?: string | null;
  legalForm: string | null;
  addressLines: string[];
  email: string | null;
  phone: string | null;
  phoneNumbers?: string[];
  representedBy?: string[];
  contentResponsible?: string[];
  contentResponsibleRole?: string | null;
  contentResponsibleAddressLines?: string[];
  website: string | null;
  registerCourt: string | null;
  registerNumber: string | null;
  vatId: string | null;
  privacyContact: string | null;
}

export interface ProductInfo {
  productName: string;
  edition: string;
  version: string;
  legalNotice: LegalNotice;
}
