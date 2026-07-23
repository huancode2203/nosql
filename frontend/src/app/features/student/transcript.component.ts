import { Component, OnInit, computed, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ApiService } from '../../core/services/api.service';
import { CourseGrade } from '../../core/models/api.models';
import { TranscriptTerm } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

interface CloResult {
  courseCode: string;
  courseName: string;
  cloCode: string;
  description: string;
  percentage: number;
  threshold: number;
  passed: boolean;
  contributingComponents: string[];
}

interface ComponentColumn {
  id: string;
  name: string;
}

@Component({
  standalone: true,
  imports: [DatePipe, PageHeaderComponent],
  templateUrl: './transcript.component.html'
})
export class TranscriptComponent implements OnInit {
  readonly terms = signal<TranscriptTerm[]>([]);
  readonly clos = signal<CloResult[]>([]);
  readonly loading = signal(true);
  readonly selectedYear = signal('all');
  readonly selectedSemester = signal('all');
  readonly resultFilter = signal('all');
  readonly search = signal('');

  readonly years = computed(() => [...new Set(this.terms().map(term => term.academicYear))]);
  readonly semesters = computed(() => {
    const selectedYear = this.selectedYear();
    const source = selectedYear === 'all' ? this.terms() : this.terms().filter(term => term.academicYear === selectedYear);
    const map = new Map<string, string>();
    source.forEach(term => map.set(term.semesterCode, term.semesterName));
    return [...map.entries()].map(([code, name]) => ({ code, name }));
  });

  readonly filteredTerms = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return this.terms()
      .filter(term => this.selectedYear() === 'all' || term.academicYear === this.selectedYear())
      .filter(term => this.selectedSemester() === 'all' || term.semesterCode === this.selectedSemester())
      .map(term => ({
        ...term,
        courses: term.courses
          .filter(course => this.resultFilter() === 'all' || (this.resultFilter() === 'passed' ? course.passed : !course.passed))
          .filter(course => !keyword || `${course.courseCode} ${course.courseName} ${course.classSectionCode}`.toLocaleLowerCase('vi').includes(keyword))
      }))
      .filter(term => term.courses.length > 0);
  });

  readonly componentColumns = computed<ComponentColumn[]>(() => {
    const map = new Map<string, string>();
    this.filteredTerms().forEach(term => term.courses.forEach(course => course.scores.forEach(score => {
      if (!map.has(score.componentId)) map.set(score.componentId, score.componentName);
    })));
    return [...map.entries()].map(([id, name]) => ({ id, name }));
  });

  readonly cloSummary = computed(() => {
    const items = this.clos();
    const passed = items.filter(item => item.passed).length;
    return { total: items.length, passed, failed: items.length - passed, percentage: items.length ? Math.round(passed * 1000 / items.length) / 10 : 0 };
  });

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    this.api.get<TranscriptTerm[]>('/student/transcript').subscribe({
      next: response => this.terms.set(response.data),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false)
    });
    this.api.get<CloResult[]>('/student/clo-results').subscribe({ next: response => this.clos.set(response.data) });
  }

  setYear(value: string): void {
    this.selectedYear.set(value);
    this.selectedSemester.set('all');
  }

  scoreFor(course: CourseGrade, componentId: string): string {
    const component = course.scores.find(score => score.componentId === componentId);
    if (!component || component.score === null || component.status !== 'Graded') return '—';
    return component.score.toFixed(Number.isInteger(component.score) ? 0 : 2);
  }

  componentTitle(course: CourseGrade, componentId: string): string {
    const component = course.scores.find(score => score.componentId === componentId);
    return component ? `${component.componentName}: ${component.score ?? 'Chưa có'} / ${component.maxScore} · ${component.weight}%` : 'Không áp dụng';
  }

  cloFor(courseCode: string): string {
    const values = this.clos().filter(item => item.courseCode === courseCode);
    if (!values.length) return 'Chưa đánh giá';
    return values.every(item => item.passed) ? 'Đạt' : `${values.filter(item => item.passed).length}/${values.length}`;
  }

  totalColumns(): number {
    return 12 + this.componentColumns().length;
  }

  exportData(): void {
    this.api.getBlob('/student/transcript/export').subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'bang-diem-toan-khoa.xlsx';
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  print(): void {
    window.print();
  }
}
