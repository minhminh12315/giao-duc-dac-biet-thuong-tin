export type FacilityType = 'MainCenter' | 'PartnerSchool'
export type ServiceType = 'AtCenter' | 'AtPartnerSchool'
export type AttendanceStatus = 'Present' | 'Absent' | 'Excused' | 'NotMarked'
export type SessionStatus = 'Scheduled' | 'Completed' | 'Cancelled'
export type StudentStatus = 'Active' | 'Inactive' | 'Graduated'
export type TuitionRateScope = 'Student' | 'ClassGroup' | 'ServiceType' | 'SystemDefault'
export type InvoiceStatus = 'Draft' | 'Issued' | 'Void'

export interface LoginResponse {
  accessToken: string
  expiresIn: number
  role: string
  displayName: string
  userId: string
  teacherProfileId?: string | null
}

export interface Facility {
  id: string
  name: string
  type: FacilityType
  address?: string
  contactPhone?: string
  travelBufferMinutesOverride?: number | null
  isActive: boolean
}

export interface Teacher {
  id: string
  userId: string
  userName: string
  email: string
  fullName: string
  phone?: string
  specialization?: string
  hireDate?: string
  isActive: boolean
}

export interface Student {
  id: string
  fullName: string
  dateOfBirth?: string
  gender?: string
  facilityId: string
  facilityName: string
  facilityType: FacilityType
  guardianName?: string
  guardianPhone?: string
  medicalNotes?: string
  status: StudentStatus
}

export interface ClassGroup {
  id: string
  name: string
  facilityId?: string | null
  facilityName?: string
  serviceType: ServiceType
  isActive: boolean
  studentIds: string[]
}

export interface ScheduleConflict {
  type: string
  severity: 'Error' | 'Warning' | 'Info'
  message: string
  gapMinutes?: number
  requiredMinutes?: number
}

export interface Session {
  id: string
  classGroupId?: string | null
  classGroupName?: string
  teacherProfileId: string
  teacherName: string
  facilityId: string
  facilityName: string
  facilityType: FacilityType
  startAt: string
  endAt: string
  title?: string
  status: SessionStatus
  attendanceFinalizedAt?: string | null
  notes?: string
  studentIds: string[]
  conflicts?: ScheduleConflict[]
}

export interface AttendanceItem {
  studentId: string
  studentName: string
  status: AttendanceStatus
  comment?: string | null
  moodOrLevel?: number | null
  tags?: string[] | null
}

export interface Progress {
  studentId: string
  studentName: string
  presentCount: number
  absentCount: number
  excusedCount: number
  attendanceRate: number
  commentCount: number
  avgMood?: number | null
  moodSeries: { at: string; mood: number }[]
  comments: { at: string; teacherName: string; content: string; moodOrLevel?: number | null; status: AttendanceStatus }[]
}

export interface TuitionRate {
  id: string
  scope: TuitionRateScope
  studentId?: string | null
  classGroupId?: string | null
  serviceType?: ServiceType | null
  amount: number
  currency: string
  effectiveFrom: string
  effectiveTo?: string | null
  isActive: boolean
}

export interface Invoice {
  id: string
  studentId: string
  studentName: string
  periodYear: number
  periodMonth: number
  totalAmount: number
  currency: string
  status: InvoiceStatus
  version: number
  generatedAt: string
  lines: { id: string; sessionId: string; sessionStart: string; unitRate: number; amount: number; description?: string }[]
}
