export const PRIVACY_SETTINGS = [
  {
    key: "showLastVisitTime",
    label: "Скрывать время последнего визита",
    description: "Вместо точного времени будет отображаться «Был недавно»",
  },
] as const;

export type PrivacyField = (typeof PRIVACY_SETTINGS)[number]["key"];
export type PrivacyForm = Record<PrivacyField, boolean>;
