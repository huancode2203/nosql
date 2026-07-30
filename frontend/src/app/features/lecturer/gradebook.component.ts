import { Component, OnInit, input, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../core/services/api.service';
import { ImportPreview } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { ToastService } from '../../core/services/toast.service';

interface ComponentDef {
  componentId: string;
  componentName: string;
  weight: number;
  maxScore: number;
}

interface StudentRow {
  studentId: string;
  studentCode: string;
  fullName: string;
  scores: Record<string, string | null>;
  rawScores?: Record<string, string | null>;
  finalScore: number;
  letterGrade: string;
  passed: boolean;
  version: number;
  dirty?: boolean;
  confirmedComponents?: string[];
  errors?: Record<string, string>;
  notes?: Record<string, string>;
}

interface Gradebook {
  classSectionId: string;
  classSectionCode: string;
  courseName: string;
  status: string;
  components: ComponentDef[];
  students: StudentRow[];
}

interface NormalizationPreview {
  value: number | null;
  displayValue?: string;
  error?: string;
  warning?: string;
  requiresConfirmation?: boolean;
}

@Component({
  standalone: true,
  imports: [FormsModule, DecimalPipe, PageHeaderComponent],
  templateUrl: './gradebook.component.html',
  styleUrl: './gradebook.component.scss'
})
export class GradebookComponent implements OnInit {
  id = input('default');
  book = signal<Gradebook | null>(null);
  loading = signal(true);
  saving = signal(false);
  importModal = signal(false);
  importPreview = signal<ImportPreview | null>(null);
  importFile: File | null = null;

  constructor(
    private api: ApiService,
    private toast: ToastService
  ) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.get(`/lecturer/classes/${this.id() || 'default'}/gradebook`).subscribe({
      next: response => {
        const book = response.data as Gradebook;

        book.students.forEach(row => {
          row.rawScores = { ...row.scores };
          row.confirmedComponents = [];
          row.errors = {};
          row.notes = {};
          row.version ??= 0;
        });

        this.book.set(book);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  trackScoreInput(
    event: Event,
    row: StudentRow,
    key: string
  ) {
    const input = event.target as HTMLInputElement;
    const original = input.value;
    const sanitized = this.sanitizeScoreText(original);

    if (original !== sanitized) {
      input.value = sanitized;
    }

    row.rawScores ??= {};
    row.errors ??= {};
    row.notes ??= {};
    row.confirmedComponents ??= [];

    row.rawScores[key] = sanitized;
    row.scores[key] = sanitized;
    row.dirty = true;

    if (original !== sanitized) {
      row.errors[key] =
        'Chỉ được nhập chữ số và tối đa một dấu phẩy hoặc dấu chấm.';
    } else {
      delete row.errors[key];
    }

    delete row.notes[key];
  }

  normalizeScore(
    row: StudentRow,
    key: string,
    maxScore: number
  ) {
    const raw = String(
      row.rawScores?.[key]
      ?? row.scores[key]
      ?? ''
    ).trim();

    const preview = this.previewNormalize(raw, maxScore);

    row.rawScores ??= {};
    row.errors ??= {};
    row.notes ??= {};
    row.confirmedComponents ??= [];

    row.rawScores[key] = raw;

    if (preview.error) {
      row.errors[key] = preview.error;
      delete row.notes[key];
      row.scores[key] = raw;
    } else {
      delete row.errors[key];

      if (preview.requiresConfirmation) {
        const accepted = window.confirm(
          preview.warning
          ?? 'Giá trị này cần được xác nhận trước khi lưu.'
        );

        if (accepted) {
          if (!row.confirmedComponents.includes(key)) {
            row.confirmedComponents.push(key);
          }
        } else {
          row.confirmedComponents =
            row.confirmedComponents.filter(item => item !== key);
          row.errors[key] = 'Giá trị chưa được xác nhận.';
        }
      } else {
        row.confirmedComponents =
          row.confirmedComponents.filter(item => item !== key);
      }

      if (preview.warning) {
        row.notes[key] = preview.warning;
      } else {
        delete row.notes[key];
      }

      row.scores[key] =
        preview.displayValue
        ?? (
          preview.value === null
            ? null
            : this.formatScore(preview.value)
        );
    }

    row.dirty = true;
    this.recalculatePreview(row);
  }

  blockInvalidScoreKey(event: KeyboardEvent) {
    const navigationKeys = new Set([
      'Backspace',
      'Delete',
      'Tab',
      'Escape',
      'Enter',
      'ArrowLeft',
      'ArrowRight',
      'ArrowUp',
      'ArrowDown',
      'Home',
      'End'
    ]);

    if (navigationKeys.has(event.key)) {
      return;
    }

    if (
      (event.ctrlKey || event.metaKey)
      && ['a', 'c', 'v', 'x', 'z', 'y']
        .includes(event.key.toLowerCase())
    ) {
      return;
    }

    if (/^\d$/.test(event.key)) {
      return;
    }

    if (event.key === '.' || event.key === ',') {
      const input = event.target as HTMLInputElement;
      const start = input.selectionStart ?? input.value.length;
      const end = input.selectionEnd ?? input.value.length;
      const withoutSelection =
        input.value.slice(0, start)
        + input.value.slice(end);

      if (!/[.,]/.test(withoutSelection)) {
        return;
      }
    }

    event.preventDefault();
  }

  blockInvalidScoreBeforeInput(event: Event) {
    const inputEvent = event as InputEvent;

    if (
      inputEvent.inputType.startsWith('delete')
      || inputEvent.inputType.startsWith('history')
      || inputEvent.data === null
    ) {
      return;
    }

    if (!/^[0-9.,]+$/.test(inputEvent.data)) {
      inputEvent.preventDefault();
    }
  }

  handleScorePaste(event: ClipboardEvent) {
    const pasted = (
      event.clipboardData?.getData('text')
      ?? ''
    ).replace(/\s+/g, '');

    if (!this.isAllowedScoreText(pasted)) {
      event.preventDefault();

      const input = event.target as HTMLInputElement;
      const sanitized = this.sanitizeScoreText(pasted);
      const start = input.selectionStart ?? input.value.length;
      const end = input.selectionEnd ?? input.value.length;

      input.setRangeText(sanitized, start, end, 'end');
      input.dispatchEvent(
        new Event('input', { bubbles: true })
      );
    }
  }

  blockScoreDrop(event: DragEvent) {
    event.preventDefault();
  }

  statusLabel(status: string) {
    const labels: Record<string, string> = {
      Draft: 'Bản nháp',
      InProgress: 'Đang nhập điểm',
      Reopened: 'Đã mở lại',
      Submitted: 'Đã gửi duyệt',
      Published: 'Đã công bố',
      Locked: 'Đã khóa'
    };

    return labels[status] ?? status;
  }

  private isAllowedScoreText(value: string) {
    return /^\d*(?:[.,]\d*)?$/.test(value);
  }

  private sanitizeScoreText(value: string) {
    const compact = value.replace(/\s+/g, '');
    let result = '';
    let separatorUsed = false;

    for (const character of compact) {
      if (/\d/.test(character)) {
        result += character;
        continue;
      }

      if ((character === '.' || character === ',') && !separatorUsed) {
        result += character;
        separatorUsed = true;
      }
    }

    return result.slice(0, 8);
  }

  private previewNormalize(
    rawInput: string,
    maxScore: number
  ): NormalizationPreview {
    const raw = rawInput.replace(/\s+/g, '');

    if (!raw) {
      return {
        value: null,
        displayValue: ''
      };
    }

    if (/[-eE]/.test(raw)) {
      return {
        value: null,
        error: 'Không cho phép điểm âm hoặc dạng số khoa học.'
      };
    }

    if (raw === '0700') {
      return {
        value: 7.1,
        displayValue: '7,1',
        requiresConfirmation: true,
        warning:
          'Hệ thống đã chuẩn hóa “0700” thành “7,1”. Vui lòng xác nhận trước khi lưu.'
      };
    }

    const normalized = raw.replace(',', '.');

    if (/^\d+\.\d+$/.test(normalized)) {
      const value = Number(normalized);
      return this.validatePreview(value, maxScore);
    }

    if (!/^\d+$/.test(raw)) {
      return {
        value: null,
        error: 'Điểm không hợp lệ.'
      };
    }

    if (raw === '0' || raw === '1' || raw === '10') {
      return this.validatePreview(Number(raw), maxScore);
    }

    let shortened = raw;
    while (shortened.length > 2 && shortened.endsWith('0')) {
      shortened = shortened.slice(0, -1);
    }

    if (shortened === '10') {
      const result = this.validatePreview(10, maxScore);
      return {
        ...result,
        displayValue: '10',
        warning:
          shortened === raw
            ? undefined
            : `Đã chuẩn hóa từ ${raw} thành 10.`
      };
    }

    if (shortened.length === 2) {
      const value = Number(shortened) / 10;
      const result = this.validatePreview(value, maxScore);

      return {
        ...result,
        displayValue: this.formatScore(value),
        warning:
          shortened === raw
            ? undefined
            : `Đã chuẩn hóa từ ${raw} thành ${this.formatScore(value)}.`
      };
    }

    const integer = Number(shortened);
    if (Number.isInteger(integer) && integer >= 0 && integer <= 10) {
      return this.validatePreview(integer, maxScore);
    }

    return {
      value: null,
      error: `Điểm phải nằm trong khoảng 0 đến ${Math.min(10, maxScore)}.`
    };
  }

  private validatePreview(
    value: number,
    maxScore: number
  ): NormalizationPreview {
    const upperBound = Math.min(10, maxScore);

    if (!Number.isFinite(value) || value < 0 || value > upperBound) {
      return {
        value: null,
        error: `Điểm phải từ 0 đến ${upperBound}.`
      };
    }

    return {
      value,
      displayValue: this.formatScore(value)
    };
  }

  private formatScore(value: number) {
    return value.toLocaleString('vi-VN', {
      maximumFractionDigits: 4
    });
  }

  recalculatePreview(row: StudentRow) {
    const book = this.book();
    if (!book) return;

    let sum = 0;

    for (const component of book.components) {
      const raw = row.rawScores?.[component.componentId]
        ?? row.scores[component.componentId]
        ?? '';

      const parsed = this.previewNormalize(raw, component.maxScore);
      const value = parsed.error ? 0 : (parsed.value ?? 0);

      sum +=
        (value / component.maxScore)
        * 10
        * (component.weight / 100);
    }

    row.finalScore = Math.round(sum * 100) / 100;
    row.letterGrade =
      sum >= 8.5 ? 'A'
      : sum >= 8 ? 'B+'
      : sum >= 7 ? 'B'
      : sum >= 6.5 ? 'C+'
      : sum >= 5.5 ? 'C'
      : sum >= 5 ? 'D+'
      : sum >= 4 ? 'D'
      : 'F';
    row.passed = sum >= 4;
  }

  save(submit = false) {
    const book = this.book();
    if (!book) return;

    const rows = submit
      ? book.students
      : book.students.filter(row => row.dirty);

    if (!rows.length) {
      this.toast.show('Chưa có ô điểm nào thay đổi', 'info');
      return;
    }

    if (rows.some(row => Object.keys(row.errors ?? {}).length > 0)) {
      this.toast.show(
        'Còn ô điểm không hợp lệ hoặc chưa được xác nhận',
        'error'
      );
      return;
    }

    this.saving.set(true);

    const students = rows.map(row => ({
      studentId: row.studentId,
      scores: row.rawScores ?? row.scores,
      confirmedComponents: row.confirmedComponents ?? [],
      version: row.version
    }));

    this.api.put(
      `/lecturer/classes/${book.classSectionId}/grades`,
      {
        students,
        publish: submit
      }
    ).subscribe({
      next: () => {
        this.toast.show(
          submit
            ? 'Đã gửi bảng điểm để quản trị viên kiểm tra'
            : 'Đã lưu bản nháp',
          'success'
        );

        book.students.forEach(row => row.dirty = false);

        if (submit) {
          book.status = 'Submitted';
        }

        this.saving.set(false);
        this.load();
      },
      error: error => {
        this.toast.show(
          error.error?.message || 'Lưu điểm thất bại',
          'error'
        );
        this.saving.set(false);
      }
    });
  }

  exportData() {
    const book = this.book();
    if (!book) return;

    this.api
      .getBlob(
        `/lecturer/classes/${book.classSectionId}/gradebook/export`
      )
      .subscribe(blob =>
        this.download(
          blob,
          `bang-diem-${book.classSectionCode}.xlsx`
        )
      );
  }

  chooseImport(files: FileList | null) {
    this.importFile = files?.item(0) || null;
    if (!this.importFile || !this.book()) return;

    const form = new FormData();
    form.append('file', this.importFile);

    this.api.postForm(
      `/lecturer/classes/${this.book()!.classSectionId}/grades/import`,
      form,
      { commit: false }
    ).subscribe({
      next: response => {
        this.importPreview.set(response.data as ImportPreview);
        this.importModal.set(true);
      },
      error: error =>
        this.toast.show(
          error.error?.message || 'Không thể đọc file',
          'error'
        )
    });
  }

  commitImport() {
    if (!this.importFile || !this.book()) return;

    const form = new FormData();
    form.append('file', this.importFile);

    this.api.postForm(
      `/lecturer/classes/${this.book()!.classSectionId}/grades/import`,
      form,
      { commit: true }
    ).subscribe({
      next: () => {
        this.toast.show('Import điểm thành công', 'success');
        this.importModal.set(false);
        this.load();
      },
      error: error =>
        this.toast.show(
          error.error?.message || 'Import thất bại',
          'error'
        )
    });
  }

  requestReopen() {
    const book = this.book();
    if (!book) return;

    const reason = prompt(
      'Lý do yêu cầu mở lại bảng điểm',
      'Cần điều chỉnh điểm sau rà soát'
    );

    if (!reason) return;

    this.api.post(
      `/lecturer/classes/${book.classSectionId}/request-reopen`,
      { reason }
    ).subscribe({
      next: () =>
        this.toast.show(
          'Đã gửi yêu cầu đến quản trị viên',
          'success'
        ),
      error: error =>
        this.toast.show(
          error.error?.message || 'Không thể gửi yêu cầu',
          'error'
        )
    });
  }

  private download(blob: Blob, name: string) {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = name;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
