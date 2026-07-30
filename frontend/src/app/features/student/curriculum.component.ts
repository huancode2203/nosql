import { NgTemplateOutlet } from '@angular/common';
import {
  Component,
  OnInit,
  computed,
  inject,
  signal
} from '@angular/core';
import { ApiService } from '../../core/services/api.service';
import {
  CurriculumCourse,
  CurriculumSemester,
  StudentCurriculum
} from '../../core/models/portal.models';
import { LoadingComponent } from '../../shared/loading.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [
    LoadingComponent,
    PageHeaderComponent,
    NgTemplateOutlet
  ],
  templateUrl: './curriculum.component.html',
  styleUrl: './curriculum.component.scss'
})
export class CurriculumComponent implements OnInit {
  private readonly api = inject(ApiService);

  readonly curriculum = signal<StudentCurriculum | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');

  readonly semesters = computed(
    () => this.curriculum()?.semesters ?? []
  );

  ngOnInit(): void {
    this.api.get<StudentCurriculum>('/student/curriculum')
      .subscribe({
        next: response => {
          this.curriculum.set(response.data);
          this.loading.set(false);
        },
        error: error => {
          this.error.set(
            error.error?.message
            || 'Không thể tải chương trình khung.'
          );
          this.loading.set(false);
        }
      });
  }

  coursesByGroup(
    semester: CurriculumSemester,
    group: 'Required' | 'Elective'
  ): CurriculumCourse[] {
    return semester.courses.filter(
      course => course.group === group
    );
  }

  semesterCredits(
    semester: CurriculumSemester
  ): number {
    return semester.requiredCredits
      + semester.electiveCredits;
  }

  courseType(course: CurriculumCourse): string {
    if (course.isCoreCourse) {
      return 'Cốt lõi';
    }

    return course.group === 'Elective'
      ? 'Tự chọn'
      : '';
  }

  statusText(
    status: CurriculumCourse['status']
  ): string {
    return {
      Passed: 'Đạt',
      Failed: 'Không đạt',
      InProgress: 'Đang học',
      NotRegistered: 'Chưa đăng ký'
    }[status];
  }

  statusIcon(
    status: CurriculumCourse['status']
  ): string {
    return {
      Passed: 'check_circle',
      Failed: 'cancel',
      InProgress: 'pending',
      NotRegistered: 'radio_button_unchecked'
    }[status];
  }

  statusClass(
    status: CurriculumCourse['status']
  ): string {
    return status.toLocaleLowerCase();
  }

  print(): void {
    window.print();
  }

  exportCsv(): void {
    const data = this.curriculum();

    if (!data) {
      return;
    }

    const rows: string[][] = [
      [
        'Học kỳ',
        'STT',
        'Tên học phần',
        'Mã học phần',
        'Loại',
        'Số tín chỉ',
        'Số tiết LT',
        'Số tiết TH',
        'Nhóm tự chọn',
        'TC bắt buộc của nhóm',
        'Kết quả'
      ]
    ];

    for (const semester of data.semesters) {
      for (const course of semester.courses) {
        rows.push([
          String(semester.semesterNumber),
          String(course.order),
          course.courseName,
          course.courseCode,
          this.courseType(course),
          String(course.credits),
          String(course.theoryPeriods),
          String(course.practicePeriods),
          String(course.electiveGroup || 0),
          String(course.requiredCreditsInGroup || ''),
          this.statusText(course.status)
        ]);
      }
    }

    const csv = rows
      .map(row =>
        row.map(value =>
          `"${value.replaceAll('"', '""')}"`
        ).join(',')
      )
      .join('\r\n');

    const blob = new Blob(
      ['\uFEFF' + csv],
      {
        type: 'text/csv;charset=utf-8'
      }
    );

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');

    anchor.href = url;
    anchor.download =
      `chuong-trinh-khung-${data.programCode}.csv`;
    anchor.click();

    URL.revokeObjectURL(url);
  }

  toggleFullscreen(): void {
    const element = document.documentElement;

    if (document.fullscreenElement) {
      void document.exitFullscreen();
      return;
    }

    void element.requestFullscreen();
  }
}
