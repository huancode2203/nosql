import { Component, OnInit, computed, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { StudentCourse } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

const DAY_NAMES: Record<string, string> = { Monday: 'Thứ Hai', Tuesday: 'Thứ Ba', Wednesday: 'Thứ Tư', Thursday: 'Thứ Năm', Friday: 'Thứ Sáu', Saturday: 'Thứ Bảy', Sunday: 'Chủ Nhật' };

@Component({
  standalone: true,
  imports: [PageHeaderComponent, RouterLink],
  template: `
  <app-page-header title="Môn học hiện tại" subtitle="Các lớp học phần sinh viên đang tham gia."></app-page-header>
  <section class="academic-filter-bar panel compact-filters">
    <label>Tìm môn học<div class="filter-input"><span class="material-symbols-outlined">search</span><input type="search" placeholder="Mã môn, tên môn, giảng viên..." [value]="search()" (input)="search.set($any($event.target).value)" /></div></label>
    <div class="schedule-summary"><span class="status-chip">{{ filteredItems().length }} lớp học phần</span></div>
  </section>
  <div class="course-grade-grid">
    @for (item of filteredItems(); track item.classSectionId) {
      <article class="panel class-card">
        <div class="panel-heading"><div><span class="eyebrow">{{ item.courseCode }}</span><h3>{{ item.courseName }}</h3><p>{{ item.classSectionCode }} · {{ item.credits }} tín chỉ</p></div><span class="badge" [class.success]="item.scoreStatus === 'Published'">{{ statusLabel(item.scoreStatus) }}</span></div>
        <div class="detail-list compact-details">
          <div><span>Giảng viên</span><b>{{ item.lecturerName }}</b></div>
          <div><span>Học kỳ</span><b>{{ item.semesterName }} · {{ item.academicYearName }}</b></div>
          @for (slot of item.schedule; track $index) { <div><span>Lịch học</span><b>{{ dayLabel(slot.dayOfWeek) }} {{ slot.startTime }} - {{ slot.endTime }} · {{ slot.room }}</b></div> }
        </div>
        <footer class="card-actions"><a class="text-button" [routerLink]="['/student/grades/course', item.courseCode]">Xem kết quả</a><a class="text-button" routerLink="/student/materials">Tài liệu</a></footer>
      </article>
    } @empty { <article class="panel empty-state"><span class="material-symbols-outlined">auto_stories</span><h3>Chưa có môn học</h3></article> }
  </div>`
})
export class CurrentCoursesComponent implements OnInit {
  readonly items = signal<StudentCourse[]>([]);
  readonly search = signal('');
  readonly filteredItems = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return keyword ? this.items().filter(item => `${item.courseCode} ${item.courseName} ${item.lecturerName} ${item.classSectionCode}`.toLocaleLowerCase('vi').includes(keyword)) : this.items();
  });
  constructor(private readonly api: ApiService) {}
  ngOnInit(): void { this.api.get<StudentCourse[]>('/student/current-courses').subscribe(response => this.items.set(response.data)); }
  dayLabel(day: string): string { return DAY_NAMES[day] || day; }
  statusLabel(status: string): string { return ({ Published: 'Đã công bố', Draft: 'Nháp', InProgress: 'Đang nhập', Locked: 'Đã khóa', Submitted: 'Đã gửi' } as Record<string, string>)[status] || status; }
}
