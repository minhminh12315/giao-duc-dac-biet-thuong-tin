# 04 — Database Schema (PostgreSQL)

## Conventions

- PK: `uuid` default `gen_random_uuid()` (extension `pgcrypto`)
- Timestamps: `timestamptz`
- Soft delete: `is_deleted boolean default false`, `deleted_at timestamptz`
- Money: `numeric(18,2)`
- Enums: PostgreSQL ENUM **hoặc** `smallint` + check (khuyến nghị **text/check** hoặc EF string conversion cho dễ migrate)

## Extensions

```sql
CREATE EXTENSION IF NOT EXISTS "pgcrypto";
```

## DDL (core)

```sql
-- USERS (có thể dùng AspNetUsers của Identity; map tương đương)
CREATE TABLE users (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_name       varchar(256) NOT NULL UNIQUE,
  email           varchar(256) NOT NULL UNIQUE,
  password_hash   text NOT NULL,
  role            varchar(32) NOT NULL CHECK (role IN ('Admin','Teacher')),
  is_active       boolean NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE teacher_profiles (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         uuid NOT NULL UNIQUE REFERENCES users(id),
  full_name       varchar(200) NOT NULL,
  phone           varchar(50),
  date_of_birth   date,
  address         text,
  specialization  varchar(200),
  hire_date       date,
  notes           text,
  is_deleted      boolean NOT NULL DEFAULT false,
  deleted_at      timestamptz,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE facilities (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name              varchar(200) NOT NULL,
  type              varchar(32) NOT NULL CHECK (type IN ('MainCenter','PartnerSchool')),
  address           text,
  contact_phone     varchar(50),
  travel_buffer_minutes_override int,
  is_active         boolean NOT NULL DEFAULT true,
  is_deleted        boolean NOT NULL DEFAULT false,
  deleted_at        timestamptz,
  created_at        timestamptz NOT NULL DEFAULT now(),
  updated_at        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE students (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  full_name       varchar(200) NOT NULL,
  date_of_birth   date,
  gender          varchar(16),
  facility_id     uuid NOT NULL REFERENCES facilities(id),
  guardian_name   varchar(200),
  guardian_phone  varchar(50),
  medical_notes   text,
  status          varchar(32) NOT NULL DEFAULT 'Active'
                  CHECK (status IN ('Active','Inactive','Graduated')),
  is_deleted      boolean NOT NULL DEFAULT false,
  deleted_at      timestamptz,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_students_facility ON students(facility_id);

CREATE TABLE teacher_documents (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  teacher_profile_id  uuid NOT NULL REFERENCES teacher_profiles(id),
  document_type       varchar(32) NOT NULL
                      CHECK (document_type IN ('Contract','Certificate','Other')),
  file_name           varchar(500) NOT NULL,
  content_type        varchar(100) NOT NULL,
  storage_path        varchar(1000) NOT NULL,
  public_url          varchar(1000),
  size_bytes          bigint NOT NULL,
  uploaded_at         timestamptz NOT NULL DEFAULT now(),
  uploaded_by_user_id uuid NOT NULL REFERENCES users(id)
);
CREATE INDEX ix_teacher_docs_teacher ON teacher_documents(teacher_profile_id);

CREATE TABLE student_documents (
  id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  student_id          uuid NOT NULL REFERENCES students(id),
  document_type       varchar(32) NOT NULL
                      CHECK (document_type IN ('MedicalReport','Assessment','Other')),
  file_name           varchar(500) NOT NULL,
  content_type        varchar(100) NOT NULL,
  storage_path        varchar(1000) NOT NULL,
  public_url          varchar(1000),
  size_bytes          bigint NOT NULL,
  uploaded_at         timestamptz NOT NULL DEFAULT now(),
  uploaded_by_user_id uuid NOT NULL REFERENCES users(id)
);
CREATE INDEX ix_student_docs_student ON student_documents(student_id);

CREATE TABLE class_groups (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  name            varchar(200) NOT NULL,
  facility_id     uuid REFERENCES facilities(id),
  service_type    varchar(32) NOT NULL
                  CHECK (service_type IN ('AtCenter','AtPartnerSchool')),
  is_active       boolean NOT NULL DEFAULT true,
  is_deleted      boolean NOT NULL DEFAULT false,
  deleted_at      timestamptz,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE class_group_students (
  class_group_id  uuid NOT NULL REFERENCES class_groups(id),
  student_id      uuid NOT NULL REFERENCES students(id),
  joined_at       timestamptz NOT NULL DEFAULT now(),
  left_at         timestamptz,
  PRIMARY KEY (class_group_id, student_id)
);

CREATE TABLE sessions (
  id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  class_group_id          uuid REFERENCES class_groups(id),
  teacher_profile_id      uuid NOT NULL REFERENCES teacher_profiles(id),
  facility_id             uuid NOT NULL REFERENCES facilities(id),
  start_at                timestamptz NOT NULL,
  end_at                  timestamptz NOT NULL,
  title                   varchar(300),
  status                  varchar(32) NOT NULL DEFAULT 'Scheduled'
                          CHECK (status IN ('Scheduled','Completed','Cancelled')),
  attendance_finalized_at timestamptz,
  notes                   text,
  source_clone_session_id uuid REFERENCES sessions(id),
  is_deleted              boolean NOT NULL DEFAULT false,
  deleted_at              timestamptz,
  created_at              timestamptz NOT NULL DEFAULT now(),
  updated_at              timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_session_time CHECK (end_at > start_at)
);
CREATE INDEX ix_sessions_teacher_time ON sessions(teacher_profile_id, start_at, end_at);
CREATE INDEX ix_sessions_facility_time ON sessions(facility_id, start_at);
CREATE INDEX ix_sessions_week ON sessions(start_at);

CREATE TABLE session_students (
  session_id  uuid NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  student_id  uuid NOT NULL REFERENCES students(id),
  is_active   boolean NOT NULL DEFAULT true,
  PRIMARY KEY (session_id, student_id)
);

CREATE TABLE attendance_records (
  id                uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  session_id        uuid NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
  student_id        uuid NOT NULL REFERENCES students(id),
  status            varchar(32) NOT NULL DEFAULT 'NotMarked'
                    CHECK (status IN ('Present','Absent','Excused','NotMarked')),
  marked_at         timestamptz,
  marked_by_user_id uuid REFERENCES users(id),
  UNIQUE (session_id, student_id)
);
CREATE INDEX ix_attendance_student ON attendance_records(student_id, status);

CREATE TABLE teaching_comments (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  attendance_record_id  uuid NOT NULL UNIQUE REFERENCES attendance_records(id) ON DELETE CASCADE,
  content               text NOT NULL,
  mood_or_level         smallint CHECK (mood_or_level BETWEEN 1 AND 5),
  tags                  jsonb,
  created_at            timestamptz NOT NULL DEFAULT now(),
  updated_at            timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE tuition_rates (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  scope           varchar(32) NOT NULL
                  CHECK (scope IN ('Student','ClassGroup','ServiceType','SystemDefault')),
  student_id      uuid REFERENCES students(id),
  class_group_id  uuid REFERENCES class_groups(id),
  service_type    varchar(32) CHECK (service_type IN ('AtCenter','AtPartnerSchool')),
  amount          numeric(18,2) NOT NULL CHECK (amount >= 0),
  currency        varchar(8) NOT NULL DEFAULT 'VND',
  effective_from  date NOT NULL,
  effective_to    date,
  is_active       boolean NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_rates_student ON tuition_rates(student_id) WHERE scope = 'Student';
CREATE INDEX ix_rates_class ON tuition_rates(class_group_id) WHERE scope = 'ClassGroup';

CREATE TABLE invoices (
  id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  student_id      uuid NOT NULL REFERENCES students(id),
  period_year     int NOT NULL,
  period_month    int NOT NULL CHECK (period_month BETWEEN 1 AND 12),
  total_amount    numeric(18,2) NOT NULL,
  currency        varchar(8) NOT NULL DEFAULT 'VND',
  status          varchar(32) NOT NULL DEFAULT 'Draft'
                  CHECK (status IN ('Draft','Issued','Void')),
  version         int NOT NULL DEFAULT 1,
  generated_at    timestamptz NOT NULL DEFAULT now(),
  generated_by    uuid REFERENCES users(id),
  UNIQUE (student_id, period_year, period_month, version)
);
CREATE INDEX ix_invoices_period ON invoices(period_year, period_month);

CREATE TABLE invoice_lines (
  id                    uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  invoice_id            uuid NOT NULL REFERENCES invoices(id) ON DELETE CASCADE,
  session_id            uuid NOT NULL REFERENCES sessions(id),
  attendance_record_id  uuid NOT NULL REFERENCES attendance_records(id),
  unit_rate             numeric(18,2) NOT NULL,
  amount                numeric(18,2) NOT NULL,
  description           varchar(500)
);

CREATE TABLE audit_logs (
  id            uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  actor_user_id uuid REFERENCES users(id),
  action        varchar(100) NOT NULL,
  entity_type   varchar(100) NOT NULL,
  entity_id     uuid,
  before_json   jsonb,
  after_json    jsonb,
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_audit_entity ON audit_logs(entity_type, entity_id);
```

## Identity note

Nếu dùng ASP.NET Identity đầy đủ: thay `users` bằng `AspNetUsers` + `AspNetRoles` + `AspNetUserRoles`. Giữ `teacher_profiles.user_id` → `AspNetUsers.Id`. Role names: `Admin`, `Teacher`.

## Seed data tối thiểu

1. Admin user
2. 1 Facility MainCenter
3. 1 Facility PartnerSchool
4. 1 SystemDefault TuitionRate
5. Config values trong `appsettings` (không nhất thiết DB)

## Migration strategy

- EF Core migrations trong `Secms.Infrastructure`
- Naming: `YYYYMMDDHHMM_Description`
- Không sửa migration đã apply trên shared env — tạo migration mới
