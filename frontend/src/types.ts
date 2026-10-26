// All API response/request shapes live HERE, in one place. They mirror the C# DTOs in
// backend/BugBoard.Api/Dtos. If the API changes, this is the only file to update.

export const ROLES = ['Tester', 'Lead'] as const
export type Role = (typeof ROLES)[number]

export const SEVERITIES = ['Low', 'Medium', 'High', 'Critical'] as const
export type Severity = (typeof SEVERITIES)[number]

export const STATUSES = ['Open', 'InProgress', 'Resolved', 'Closed'] as const
export type BugStatus = (typeof STATUSES)[number]

export interface User {
  id: number
  email: string
  displayName: string
  role: Role
}

export interface LoginResponse {
  accessToken: string
  expiresAt: string // ISO-8601 UTC timestamp
  user: User
}

export interface Bug {
  id: number
  title: string
  description: string | null
  severity: Severity
  status: BugStatus
  reportedById: number
  reportedByName: string
  assignedToId: number | null
  assignedToName: string | null
  createdAt: string
  updatedAt: string
}

export interface PagedResult<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

/** Body for POST /api/bugs */
export interface CreateBugInput {
  title: string
  description: string | null
  severity: Severity
  assignedToId: number | null
}

/** Body for PUT /api/bugs/{id} */
export interface UpdateBugInput extends CreateBugInput {
  status: BugStatus
}

export interface BugListParams {
  status?: BugStatus | ''
  severity?: Severity | ''
  search?: string
  page?: number
  pageSize?: number
}

/** Field name -> list of messages. Matches ValidationProblemDetails.errors from the API. */
export type FieldErrors = Record<string, string[]>
