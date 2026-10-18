export type ActivityStatusValue = 'Upcoming' | 'Ongoing' | 'Finished'
export type ParticipantSourceValue = 'Manual' | 'Self' | 'Import'

export interface ActivityModel {
  id: string
  name: string
  startTime: string
  endTime: string
  location: string
  description?: string | null
  status: ActivityStatusValue
  selfRegistrationEnabled: boolean
  createdBy?: string | null
  participantCount: number
  createdAt: string
  updatedAt: string
}

export interface ActivityCreateDto {
  name: string
  startTime: string
  endTime: string
  location: string
  description?: string | null
}

export interface ParticipantModel {
  id: string
  activityId: string
  name: string
  studentId: string
  academy: string
  className: string
  source: ParticipantSourceValue
  createdAt: string
  updatedAt: string
}

export interface ParticipantCreateDto {
  name: string
  studentId: string
  academy: string
  className: string
}

export interface ParticipantPage {
  items: ParticipantModel[]
  total: number
}

export interface ImportResult {
  totalRows: number
  successCount: number
  failCount: number
  errors: string[]
  previewItems: ParticipantModel[]
}

export interface ActivityOperationLog {
  id: number
  activityId?: string | null
  operatorId?: string | null
  action: string
  detail?: string | null
  createdAt: string
}