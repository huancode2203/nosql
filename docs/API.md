# API v1

- Auth: `POST /auth/login`, `POST /auth/logout`, `GET /auth/me`.
- Admin: dashboard; CRUD `users`, `students`, `lecturers`, `courses`, `class-sections`; backup list/create/restore.
- Lecturer: dashboard; `GET /lecturer/classes/{id}/gradebook`; `PUT /lecturer/classes/{id}/grades`.
- Student: dashboard; grades theo năm/học kỳ; CLO results.

Response dùng `ApiResponse<T>` với `success`, `message`, `data`, `errors`, `timestamp`. Danh sách dùng `PagedResult<T>`.
