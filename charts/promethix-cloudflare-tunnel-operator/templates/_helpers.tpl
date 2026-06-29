{{- define "promethix-cloudflare-tunnel-operator.name" -}}
promethix-cloudflare-tunnel-operator
{{- end -}}

{{- define "promethix-cloudflare-tunnel-operator.namespace" -}}
{{- default .Release.Namespace .Values.namespaceOverride -}}
{{- end -}}

{{- define "promethix-cloudflare-tunnel-operator.serviceAccountName" -}}
{{- if .Values.serviceAccount.name -}}
{{- .Values.serviceAccount.name -}}
{{- else -}}
{{- include "promethix-cloudflare-tunnel-operator.name" . -}}
{{- end -}}
{{- end -}}

{{- define "promethix-cloudflare-tunnel-operator.webhookIssuerName" -}}
{{- if .Values.webhook.enabled -}}
{{- required "webhook.certificate.issuerRef.name is required when webhook.enabled=true" .Values.webhook.certificate.issuerRef.name -}}
{{- else -}}
{{- .Values.webhook.certificate.issuerRef.name -}}
{{- end -}}
{{- end -}}
