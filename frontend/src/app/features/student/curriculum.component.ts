import { NgTemplateOutlet } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ApiService } from '../../core/services/api.service';
import { CurriculumCourse, CurriculumSemester, StudentCurriculum } from '../../core/models/portal.models';
import { LoadingComponent } from '../../shared/loading.component';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [LoadingComponent, PageHeaderComponent, NgTemplateOutlet],
  template: `
    <app-page-header
      title="Chương trình khung"
      subtitle="Chương trình đào tạo Khoa Công nghệ Thông tin theo đúng cấu trúc 151 tín chỉ">
      <button type="button" class="secondary-button" (click)="print()">
        <span class="material-symbols-outlined">print</span> In chương trình
      </button>
    </app-page-header>

    @if (loading()) {
      <app-loading />
    } @else if (curriculum(); as data) {
      <section class="curriculum-summary panel">
        <div class="curriculum-program-info">
          <span class="eyebrow">{{ data.programCode }} · phiên bản {{ data.curriculumVersion }}</span>
          <h2>{{ data.programName }}</h2>
          <p>{{ data.facultyName }} · Hệ {{ data.educationLevel }} · Khóa áp dụng {{ data.applicableCohort }}</p>
        </div>
        <div class="curriculum-progress-wrap">
          <div class="curriculum-progress-label"><span>Tiến độ tích lũy</span><strong>{{ data.completedCredits }}/{{ data.requiredCredits }} TC</strong></div>
          <div class="curriculum-progress"><i [style.width.%]="data.progressPercent"></i></div>
          <small>{{ data.progressPercent }}% chương trình</small>
        </div>
        <div class="curriculum-total-grid">
          <div><span>Tổng tín chỉ yêu cầu</span><strong>{{ data.requiredCredits }}</strong></div>
          <div><span>Tín chỉ bắt buộc</span><strong>{{ data.requiredCompulsoryCredits }}</strong></div>
          <div><span>Tín chỉ tự chọn</span><strong>{{ data.requiredElectiveCredits }}</strong></div>
        </div>
      </section>

      <section class="curriculum-toolbar panel">
        <label>
          <span>Học kỳ</span>
          <select [value]="selectedSemester()" (change)="changeSemester($event)">
            <option value="all">Tất cả học kỳ</option>
            @for (semester of data.semesters; track semester.semesterNumber) {
              <option [value]="semester.semesterNumber">Học kỳ {{ semester.semesterNumber }}</option>
            }
          </select>
        </label>
        <label class="curriculum-search">
          <span>Tìm học phần</span>
          <input type="search" placeholder="Nhập mã hoặc tên học phần" (input)="changeSearch($event)" />
        </label>
        <div class="curriculum-legend">
          <span><i class="legend-dot passed"></i> Đạt</span>
          <span><i class="legend-dot failed"></i> Không đạt</span>
          <span><i class="legend-dot progress"></i> Đang học</span>
          <span><i class="legend-dot none"></i> Chưa đăng ký</span>
        </div>
      </section>

      <section class="curriculum-table-card panel">
        <div class="curriculum-table-scroll">
          <table class="curriculum-table">
            <thead>
              <tr>
                <th>STT</th>
                <th>Tên môn học / Học phần</th>
                <th>Mã học phần</th>
                <th>Số TC</th>
                <th>Số tiết LT</th>
                <th>Số tiết TH</th>
                <th>Nhóm tự chọn</th>
                <th>TC bắt buộc của nhóm</th>
                <th>Kết quả</th>
              </tr>
            </thead>
            <tbody>
              @for (semester of filteredSemesters(); track semester.semesterNumber) {
                <tr class="semester-row">
                  <td colspan="9">
                    <strong>Học kỳ {{ semester.semesterNumber }}</strong>
                    <span>{{ semester.requiredCredits + semester.electiveCredits }} tín chỉ</span>
                  </td>
                </tr>
                <tr class="course-group-row"><td colspan="9">Học phần bắt buộc · {{ semester.requiredCredits }} tín chỉ</td></tr>
                @for (course of coursesByGroup(semester, 'Required'); track course.courseCode) {
                  <ng-container [ngTemplateOutlet]="courseRow" [ngTemplateOutletContext]="{ course: course }"></ng-container>
                }
                @if (coursesByGroup(semester, 'Elective').length > 0) {
                  <tr class="course-group-row elective"><td colspan="9">Học phần tự chọn · yêu cầu {{ semester.electiveCredits }} tín chỉ</td></tr>
                  @for (course of coursesByGroup(semester, 'Elective'); track course.courseCode) {
                    <ng-container [ngTemplateOutlet]="courseRow" [ngTemplateOutletContext]="{ course: course }"></ng-container>
                  }
                }
              }
            </tbody>
          </table>
        </div>

        <ng-template #courseRow let-course="course">
          <tr [class.curriculum-selected]="course.isSelected">
            <td>{{ course.order }}</td>
            <td class="course-name-cell">
              <strong>{{ course.courseName }}</strong>
              @if (course.excludeFromGpa) { <span class="no-gpa-mark">*</span> }
              @if (course.isCoreCourse) { <small>Học phần cốt lõi</small> }
            </td>
            <td><code>{{ course.courseCode }}</code></td>
            <td>{{ course.credits }}</td>
            <td>{{ course.theoryPeriods }}</td>
            <td>{{ course.practicePeriods }}</td>
            <td>{{ course.electiveGroup || 0 }}</td>
            <td>{{ course.requiredCreditsInGroup || '' }}</td>
            <td>
              <span [class]="'curriculum-status ' + statusClass(course.status)">{{ statusText(course.status) }}</span>
              @if (course.finalScore !== undefined && course.finalScore !== null) { <small class="final-score">{{ course.finalScore }}/10</small> }
            </td>
          </tr>
        </ng-template>
      </section>

      <div class="curriculum-note">
        <strong>Ghi chú:</strong> Học phần có dấu <span>*</span> không được tính vào điểm trung bình chung tích lũy. Các lựa chọn tự chọn mặc định chỉ dùng để minh họa lộ trình 151 tín chỉ.
      </div>
    } @else {
      <div class="empty panel">Không tìm thấy chương trình khung của sinh viên.</div>
    }
  `
})
export class CurriculumComponent implements OnInit {
  private readonly api = inject(ApiService);
  readonly curriculum = signal<StudentCurriculum | null>(null);
  readonly loading = signal(true);
  readonly selectedSemester = signal('all');
  readonly search = signal('');

  readonly filteredSemesters = computed(() => {
    const data = this.curriculum();
    if (!data) return [];
    const semester = this.selectedSemester();
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return data.semesters
      .filter(item => semester === 'all' || item.semesterNumber === Number(semester))
      .map(item => ({
        ...item,
        courses: item.courses.filter(course => !keyword || `${course.courseCode} ${course.courseName}`.toLocaleLowerCase('vi').includes(keyword))
      }))
      .filter(item => item.courses.length > 0);
  });

  ngOnInit(): void {
    this.api.get<StudentCurriculum>('/student/curriculum').subscribe({
      next: response => this.curriculum.set(response.data),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false)
    });
  }

  coursesByGroup(semester: CurriculumSemester, group: 'Required' | 'Elective'): CurriculumCourse[] {
    return semester.courses.filter(course => course.group === group);
  }

  changeSemester(event: Event): void {
    this.selectedSemester.set((event.target as HTMLSelectElement).value);
  }

  changeSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
  }

  statusText(status: CurriculumCourse['status']): string {
    return { Passed: 'Đạt', Failed: 'Không đạt', InProgress: 'Đang học', NotRegistered: 'Chưa đăng ký' }[status];
  }

  statusClass(status: CurriculumCourse['status']): string {
    return status.toLocaleLowerCase();
  }

  print(): void { window.print(); }
}
