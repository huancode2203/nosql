import {
  Component,
  ElementRef,
  OnInit,
  ViewChild,
  computed,
  signal
} from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { CourseGrade } from '../../core/models/api.models';
import {
  StudentCurriculum,
  TranscriptTerm
} from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';

interface OutcomeRequirement {
  order: number;
  type: string;
  regulation: string;
  submitted: string;
  confirmed: boolean;
}

@Component({
  standalone: true,
  imports: [
    DecimalPipe,
    PageHeaderComponent
  ],
  templateUrl: './grades.component.html',
  styleUrl: './grades.component.scss'
})
export class GradesComponent implements OnInit {
  @ViewChild('transcriptScroll')
  private transcriptScroll?: ElementRef<HTMLDivElement>;

  readonly terms = signal<TranscriptTerm[]>([]);
  readonly curriculum = signal<StudentCurriculum | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly expandedCourseKey = signal('');

  readonly outcomes: OutcomeRequirement[] = [
    {
      order: 1,
      type: 'Chuẩn năng lực Ngoại ngữ',
      regulation: 'Theo quy định chuẩn đầu ra của chương trình đào tạo',
      submitted: 'Chưa cập nhật',
      confirmed: false
    },
    {
      order: 2,
      type: 'Chứng chỉ Giáo dục nghề nghiệp và công tác xã hội',
      regulation: 'Theo quy định của nhà trường',
      submitted: 'Chưa cập nhật',
      confirmed: false
    },
    {
      order: 3,
      type: 'Đánh giá chuẩn đầu ra chương trình đào tạo',
      regulation: 'Hoàn thành đánh giá trước khi xét tốt nghiệp',
      submitted: 'Chưa cập nhật',
      confirmed: false
    },
    {
      order: 4,
      type: 'Đối chiếu văn bằng đầu vào',
      regulation: 'Đối chiếu bằng tốt nghiệp THPT hoặc tương đương',
      submitted: 'Chưa cập nhật',
      confirmed: false
    }
  ];

  readonly actualCredits = computed(() =>
    this.terms().reduce(
      (total, term) => total + term.totalCredits,
      0
    )
  );

  readonly completedCredits = computed(() =>
    this.curriculum()?.completedCredits
    ?? this.terms().reduce(
      (total, term) => total + term.passedCredits,
      0
    )
  );

  readonly requiredCredits = computed(() =>
    this.curriculum()?.requiredCredits ?? 0
  );

  readonly cumulativeAverage10 = computed(() =>
    this.weightedTermAverage('average10')
  );

  readonly cumulativeGpa = computed(() =>
    this.weightedTermAverage('gpa')
  );

  readonly graduationStatus = computed(() => {
    const required = this.requiredCredits();

    if (required <= 0) {
      return 'Chưa xác định';
    }

    if (this.completedCredits() < required) {
      return 'Đang tích lũy';
    }

    return this.rankFromGpa(this.cumulativeGpa());
  });

  constructor(
    private readonly api: ApiService
  ) {}

  ngOnInit(): void {
    forkJoin({
      transcript:
        this.api.get<TranscriptTerm[]>('/student/transcript'),
      curriculum:
        this.api.get<StudentCurriculum>('/student/curriculum')
    }).subscribe({
      next: ({ transcript, curriculum }) => {
        this.terms.set(transcript.data ?? []);
        this.curriculum.set(curriculum.data ?? null);
        this.loading.set(false);

        setTimeout(() => this.resetTranscriptPosition());
      },
      error: error => {
        this.error.set(
          error.error?.message
          || 'Không thể tải kết quả học tập.'
        );
        this.loading.set(false);
      }
    });
  }

  exportTranscript(): void {
    this.api.getBlob('/student/transcript/export')
      .subscribe(blob => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');

        anchor.href = url;
        anchor.download = 'bang-diem-toan-khoa.xlsx';
        anchor.click();

        URL.revokeObjectURL(url);
      });
  }

  private resetTranscriptPosition(): void {
    const element = this.transcriptScroll?.nativeElement;

    if (element) {
      element.scrollLeft = 0;
      element.scrollTop = 0;
    }
  }

  courseKey(
    term: TranscriptTerm,
    course: CourseGrade
  ): string {
    return [
      term.academicYear,
      term.semesterCode,
      course.courseId,
      course.classSectionCode
    ].join('-');
  }

  toggleCourse(
    term: TranscriptTerm,
    course: CourseGrade
  ): void {
    const key = this.courseKey(term, course);

    this.expandedCourseKey.set(
      this.expandedCourseKey() === key
        ? ''
        : key
    );
  }

  scoreByKeywords(
    course: CourseGrade,
    keywords: string[]
  ): number | null {
    const component = course.scores.find(score => {
      const text =
        `${score.componentId} ${score.componentName}`
          .toLocaleLowerCase('vi');

      return keywords.some(keyword =>
        text.includes(keyword)
      );
    });

    return component?.score ?? null;
  }

  midtermScore(course: CourseGrade): number | null {
    return this.scoreByKeywords(
      course,
      ['giữa kỳ', 'giua ky', 'midterm']
    );
  }

  frequentScore(course: CourseGrade): number | null {
    return this.scoreByKeywords(
      course,
      [
        'chuyên cần',
        'chuyen can',
        'thường xuyên',
        'thuong xuyen',
        'attendance'
      ]
    );
  }

  assignmentScore(course: CourseGrade): number | null {
    return this.scoreByKeywords(
      course,
      [
        'bài tập',
        'bai tap',
        'tiểu luận',
        'tieu luan',
        'btl',
        'project',
        'assignment'
      ]
    );
  }

  finalExamScore(course: CourseGrade): number | null {
    return this.scoreByKeywords(
      course,
      [
        'cuối kỳ',
        'cuoi ky',
        'thi cuối',
        'final'
      ]
    );
  }

  regularAverage(course: CourseGrade): number | null {
    const regularComponents = course.scores.filter(score => {
      const text =
        `${score.componentId} ${score.componentName}`
          .toLocaleLowerCase('vi');

      return ![
        'cuối kỳ',
        'cuoi ky',
        'thi cuối',
        'final'
      ].some(keyword => text.includes(keyword))
        && score.score !== null
        && score.score !== undefined;
    });

    if (regularComponents.length === 0) {
      return null;
    }

    const totalWeight = regularComponents.reduce(
      (total, score) => total + score.weight,
      0
    );

    if (totalWeight <= 0) {
      return regularComponents.reduce(
        (total, score) => total + (score.score ?? 0),
        0
      ) / regularComponents.length;
    }

    return regularComponents.reduce(
      (total, score) =>
        total + (score.score ?? 0) * score.weight,
      0
    ) / totalWeight;
  }

  classification(course: CourseGrade): string {
    if (course.classification) {
      return course.classification;
    }

    if (course.gradePoint >= 3.6) {
      return 'Xuất sắc';
    }

    if (course.gradePoint >= 3.2) {
      return 'Giỏi';
    }

    if (course.gradePoint >= 2.5) {
      return 'Khá';
    }

    if (course.gradePoint >= 2) {
      return 'Trung bình';
    }

    return 'Kém';
  }

  componentFormula(course: CourseGrade): string {
    return course.scores
      .map(score =>
        `${score.componentName}: `
        + `${score.score ?? '—'}/${score.maxScore} `
        + `× ${score.weight}%`
      )
      .join(' + ');
  }

  private weightedTermAverage(
    field: 'average10' | 'gpa'
  ): number {
    const gradedTerms = this.terms().filter(
      term => term.totalCredits > 0
    );

    const credits = gradedTerms.reduce(
      (total, term) => total + term.totalCredits,
      0
    );

    if (credits <= 0) {
      return 0;
    }

    return gradedTerms.reduce(
      (total, term) =>
        total + term[field] * term.totalCredits,
      0
    ) / credits;
  }

  private rankFromGpa(gpa: number): string {
    if (gpa >= 3.6) {
      return 'Xuất sắc';
    }

    if (gpa >= 3.2) {
      return 'Giỏi';
    }

    if (gpa >= 2.5) {
      return 'Khá';
    }

    if (gpa >= 2) {
      return 'Trung bình';
    }

    return 'Chưa đạt';
  }
}
