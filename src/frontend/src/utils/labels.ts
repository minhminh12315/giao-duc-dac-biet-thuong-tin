import type {
  AttendanceStatus,
  InvoiceStatus,
  SessionStatus,
  StudentStatus,
  TuitionRateScope,
} from '../types'

export function studentStatusLabel(s: StudentStatus | string) {
  switch (s) {
    case 'Active':
      return 'Đang học'
    case 'Inactive':
      return 'Ngừng'
    case 'Graduated':
      return 'Đã tốt nghiệp'
    default:
      return s
  }
}

export function invoiceStatusLabel(s: InvoiceStatus | string) {
  switch (s) {
    case 'Draft':
      return 'Nháp'
    case 'Issued':
      return 'Đã phát hành'
    case 'Void':
      return 'Đã hủy'
    default:
      return s
  }
}

export function sessionStatusLabel(s: SessionStatus | string) {
  switch (s) {
    case 'Scheduled':
      return 'Đã xếp lịch'
    case 'Completed':
      return 'Hoàn thành'
    case 'Cancelled':
      return 'Đã hủy'
    default:
      return s
  }
}

export function attendanceStatusLabel(s: AttendanceStatus | string) {
  switch (s) {
    case 'Present':
      return 'Có mặt'
    case 'Absent':
      return 'Nghỉ'
    case 'Excused':
      return 'Có phép'
    case 'NotMarked':
      return 'Chưa điểm danh'
    default:
      return s
  }
}

export function rateScopeLabel(s: TuitionRateScope | string) {
  switch (s) {
    case 'SystemDefault':
      return 'Mặc định hệ thống'
    case 'ServiceType':
      return 'Loại dịch vụ'
    case 'ClassGroup':
      return 'Lớp / Nhóm'
    case 'Student':
      return 'Học sinh'
    default:
      return s
  }
}
