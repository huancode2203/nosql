import { Component, OnInit, computed, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { UserProfile } from '../../core/models/portal.models';
import { PageHeaderComponent } from '../../shared/page-header.component';
import { StatCardComponent } from '../../shared/stat-card.component';

interface GpaResult {
  gpa: number;
  average10: number;
  totalCredits: number;
  passedCredits: number;
  classification: string;
}

@Component({
  standalone: true,
  imports: [PageHeaderComponent, StatCardComponent],
  template: `
  <app-page-header title="Điểm trung bình và tiến độ" subtitle="GPA chỉ tính các học phần đã công bố và được tổng hợp bằng MongoDB Aggregation."></app-page-header>
  @if (data(); as g) {
    <section class="stats-grid">
      <app-stat-card label="GPA tích lũy" [value]="g.gpa" icon="insights" [trend]="g.classification" />
      <app-stat-card label="Trung bình hệ 10" [value]="g.average10" icon="calculate" tone="success" />
      <app-stat-card label="Tín chỉ tính GPA" [value]="g.totalCredits" icon="menu_book" tone="warning" />
      <app-stat-card label="Tín chỉ đã đạt" [value]="g.passedCredits" icon="workspace_premium" tone="success" />
    </section>
    <section class="gpa-layout">
      <article class="panel progress-card">
        <div class="panel-heading"><div><span class="eyebrow">TIẾN ĐỘ CHƯƠNG TRÌNH</span><h3>{{ g.passedCredits }} / {{ requiredCredits() }} tín chỉ</h3></div><strong>{{ progressPercent() }}%</strong></div>
        <div class="progress-donut large" [style.--progress]="progressPercent() + '%'"><div><strong>{{ progressPercent() }}%</strong><span>đã hoàn thành</span></div></div>
        <div class="progress"><i [style.width.%]="progressPercent()"></i></div>
      </article>
      <article class="panel">
        <div class="panel-heading"><div><span class="eyebrow">TỔNG HỢP</span><h3>Tình trạng học tập</h3></div></div>
        <div class="status-overview">
          <div><span>Xếp loại hiện tại</span><strong>{{ g.classification }}</strong></div>
          <div><span>Tín chỉ còn lại</span><strong>{{ remainingCredits() }}</strong></div>
          <div><span>Tỷ lệ tín chỉ đạt</span><strong>{{ creditPassRate() }}%</strong></div>
          <div><span>Ngành đào tạo</span><strong>{{ profile()?.programName || '—' }}</strong></div>
        </div>
      </article>
    </section>
  } @else {
    <article class="panel empty-state"><span class="material-symbols-outlined spin">progress_activity</span><h3>Đang tính GPA...</h3></article>
  }`
})
export class GpaComponent implements OnInit {
  readonly data = signal<GpaResult | null>(null);
  readonly profile = signal<UserProfile | null>(null);
  readonly requiredCredits = computed(() => Math.max(1, this.profile()?.requiredCredits || 130));
  readonly progressPercent = computed(() => Math.min(100, Math.round((this.data()?.passedCredits || 0) * 1000 / this.requiredCredits()) / 10));
  readonly remainingCredits = computed(() => Math.max(0, this.requiredCredits() - (this.data()?.passedCredits || 0)));
  readonly creditPassRate = computed(() => {
    const gpa = this.data();
    return gpa?.totalCredits ? Math.round(gpa.passedCredits * 1000 / gpa.totalCredits) / 10 : 0;
  });

  constructor(private readonly api: ApiService) {}

  ngOnInit(): void {
    forkJoin({ gpa: this.api.get<GpaResult>('/student/gpa'), profile: this.api.get<UserProfile>('/profile') })
      .subscribe(({ gpa, profile }) => {
        this.data.set(gpa.data);
        this.profile.set(profile.data);
      });
  }
}
