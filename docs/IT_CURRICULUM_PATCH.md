# IT Curriculum and Portal Patch

## Curriculum source

The seed follows the uploaded five-page Information Technology curriculum framework.

- 8 semesters.
- 151 program credits.
- 128 compulsory credits.
- 23 elective credits.
- Courses marked with `*` remain completion requirements but are excluded from GPA.
- Course codes and student codes are stored as strings so leading zeroes are preserved.

## Seeded data

- Faculty: `CNTT - Khoa Công nghệ Thông tin`.
- Programs: `CNTT2023`, `CNTT2024`, `CNTT2025`.
- 106 course options, including compulsory, elective and physical-education alternatives.
- 120 students with numeric student-code strings and realistic Vietnamese names.
- 12 lecturers with realistic names and academic titles.
- Cohorts 2023, 2024 and 2025 with multiple administrative classes.
- Historical published terms and one incomplete current term for each cohort.
- Current incomplete terms deliberately hide the semester score diagram.

## Student portal additions

- `/student/curriculum`: curriculum table grouped by semester and course group.
- `/api/v1/student/curriculum`: curriculum and progress data.
- `/api/v1/student/semester-options`: semester selector options.
- `/api/v1/student/semester-average-chart`: final score chart data.
- The dashboard selects the latest completed semester by default.
- The chart uses final scores on the 10-point scale and a credit-weighted semester average.
- Courses excluded from GPA are marked and omitted from the average.
- The chart is hidden when any course in the selected semester is not fully graded and published.

## Demo accounts

- Admin: `admin@lms.edu.vn` / `Lms@123456`
- Lecturer: `gv001@lms.edu.vn` / `Lms@123456`
- Student: `2001230282@lms.edu.vn` / `Lms@123456`
- Student username: `2001230282`
