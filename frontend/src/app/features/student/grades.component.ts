import { Component, OnInit, computed, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ApiService } from '../../core/services/api.service';
import { CourseGrade } from '../../core/models/api.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

@Component({
  standalone: true,
  imports: [PageHeaderComponent],
  templateUrl: './grades.component.html'
})
export class GradesComponent implements OnInit {
  readonly grades = signal<CourseGrade[]>([]);
  readonly selected = signal<CourseGrade | null>(null);
  readonly search = signal('');
  readonly filteredGrades = computed(() => {
    const keyword = this.search().trim().toLocaleLowerCase('vi');
    return keyword ? this.grades().filter(item => `${item.courseCode} ${item.courseName} ${item.classSectionCode} ${item.lecturerName}`.toLocaleLowerCase('vi').includes(keyword)) : this.grades();
  });

  constructor(private readonly api: ApiService, private readonly route: ActivatedRoute) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    this.api.get<CourseGrade[]>('/student/grades').subscribe(response => {
      this.grades.set(response.data);
      if (id) this.selected.set(response.data.find(item => item.courseId === id || item.courseCode === id || item.classSectionCode === id) || null);
    });
  }

  exportTranscript(): void {
    this.api.getBlob('/student/transcript/export').subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'bang-diem-toan-khoa.xlsx';
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  formula(grade: CourseGrade): string {
    return grade.scores.map(score => `${score.score ?? 0}/${score.maxScore} × 10 × ${score.weight}%`).join(' + ');
  }
}
