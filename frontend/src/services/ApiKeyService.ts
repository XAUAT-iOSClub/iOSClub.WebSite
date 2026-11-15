import { apiRequest } from './ApiService'
import { url } from './Url'

export const apiKeyPermissions = [
  { value: 1, label: '只读' },
  { value: 2, label: '读写' },
] as const

export type ApiKeyPermission = typeof apiKeyPermissions[number]['value']

export interface ApiKeySummary {
  id: string
  name: string
  permission: ApiKeyPermission
  createdAt: string
  expiresAt: string | null
}

export type CreateApiKey = Pick<ApiKeySummary, 'name' | 'permission' | 'expiresAt'>

export interface ApiKeyCredentials {
  id: string
  secret: string
}

const endpoint = `${url}/ApiKey`

export const apiKeyService = {
  list: () => apiRequest<ApiKeySummary[]>({ url: endpoint }),
  create: (request: CreateApiKey) => apiRequest<ApiKeyCredentials>({
    url: endpoint, method: 'POST', body: JSON.stringify(request),
  }),
  revoke: (id: string) => apiRequest<void>({ url: `${endpoint}/${id}`, method: 'DELETE' }),
}
