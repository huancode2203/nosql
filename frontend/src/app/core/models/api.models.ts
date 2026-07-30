export type Role = 'Admin' | 'Lecturer' | 'Student';

export interface ApiError {
  field?: string;
  message: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errors?: ApiError[];
  timestamp: string;
}

export interface PagedResult<T> {
  items: T[];
  pageNumber: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface CurrentUser {
  id: string;
  username: string;
  email: string;
  fullName: string;
  role: Role;
  avatarUrl?: string;
  permissions: string[];
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: CurrentUser;
}

export interface DashboardCard {
  label: string;
  value: number | string;
  icon: string;
  trend?: string;
  tone?: 'primary' | 'success' | 'warning' | 'danger';
}

export interface DashboardChartItem {
  label: string;
  value: number;
}

export interface DashboardActivity {
  title: string;
  description: string;
  time: string;
  icon: string;
}

export interface DashboardAlert {
  title: string;
  detail: string;
  tone: string;
}

export interface DashboardData {
  cards: DashboardCard[];
  gradeDistribution: DashboardChartItem[];
  recentActivities: DashboardActivity[];
  alerts: DashboardAlert[];
}

export interface GradeComponent {
  componentId: string;
  componentName: string;
  weight: number;
  maxScore: number;
  score: number | null;
  status: string;
}

export interface CourseGrade {
  courseId: string;
  courseCode: string;
  courseName: string;
  credits: number;
  classSectionCode: string;
  lecturerName: string;
  scores: GradeComponent[];
  finalScore: number;
  letterGrade: string;
  gradePoint: number;
  classification: string;
  passed: boolean;
  publishedAt?: string;
}
