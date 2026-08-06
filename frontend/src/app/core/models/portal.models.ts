import { CourseGrade, DashboardCard } from './api.models';

export interface ScheduleSlot { dayOfWeek: string; startTime: string; endTime: string; room: string; }
export interface LecturerClass { id: string; classSectionCode: string; courseCode: string; courseName: string; academicYearName: string; semesterName: string; studentCount: number; gradeStatus: string; schedule: ScheduleSlot[]; startDate: string; endDate: string; }
export interface ClassStudent { id: string; studentCode: string; fullName: string; email: string; administrativeClass: string; status: string; }
export interface ChartItem { label: string; value: number; }
export interface ScoreStudent { studentId: string; studentCode: string; fullName: string; finalScore: number; letterGrade: string; passed: boolean; }
export interface ClassStatistics { classSectionId: string; classSectionCode: string; courseName: string; studentCount: number; average: number; highest: number; lowest: number; median: number; standardDeviation: number; passed: number; failed: number; passRate: number; distribution: ChartItem[]; topStudents: ScoreStudent[]; atRiskStudents: ScoreStudent[]; }
export interface ClassCloStatistic { cloCode: string; description: string; averagePercentage: number; threshold: number; passedStudents: number; totalStudents: number; passRate: number; }
export interface MaterialItem { id: string; classSectionId: string; classSectionCode: string; courseCode: string; courseName: string; title: string; description: string; category: string; chapter: string; resourceType: string; resourceUrl: string; visibleFrom: string; visibleUntil?: string; viewCount: number; downloadCount: number; status: string; }
export interface AssignmentItem { id: string; classSectionId: string; classSectionCode: string; courseCode: string; courseName: string; title: string; content: string; attachmentUrl: string; maxScore: number; openAt: string; dueAt: string; allowLate: boolean; latePenaltyPercent: number; cloCodes: string[]; linkedComponentId?: string; status: string; submissionCount: number; studentSubmissionStatus?: string; studentScore?: number; studentFeedback?: string; studentResubmissionAllowed?: boolean; }
export interface SubmissionFile { originalName: string; storedName: string; url: string; sizeBytes: number; mimeType: string; }
export interface SubmissionItem { id: string; assignmentId: string; studentId: string; studentCode: string; studentName: string; textContent: string; files: SubmissionFile[]; submittedAt: string; isLate: boolean; status: string; score?: number; feedback: string; resubmissionAllowed: boolean; }
export interface StudentCourse { classSectionId: string; classSectionCode: string; courseCode: string; courseName: string; credits: number; lecturerName: string; academicYearName: string; semesterCode: string; semesterName: string; scoreStatus: string; schedule: ScheduleSlot[]; }
export interface TranscriptTerm { academicYear: string; semesterCode: string; semesterName: string; courses: CourseGrade[]; gpa: number; average10: number; totalCredits: number; passedCredits: number; }
export interface ScheduleItem { type: string; courseCode: string; courseName: string; classSectionCode: string; date?: string; dayOfWeek: string; startTime: string; endTime: string; room: string; lecturerName: string; note: string; }
export interface UserProfile { id: string; username: string; email: string; fullName: string; role: string; status: string; avatarUrl?: string; secondaryEmail?: string; phone: string; address: string; dateOfBirth?: string; studentCode?: string; lecturerCode?: string; facultyName: string; programName: string; lastLoginAt?: string; gender: string; cohort: string; administrativeClass: string; requiredCredits: number; degree: string; jobTitle: string; department: string; }
export interface ImportRow { rowNumber: number; valid: boolean; errors: string[]; data: Record<string, unknown>; }
export interface ImportPreview { totalRows: number; validRows: number; invalidRows: number; rows: ImportRow[]; }
export interface CloDefinition { cloCode: string; name: string; description: string; bloomLevel: string; threshold: number; weight: number; active: boolean; }
export interface CloMapping { cloCode: string; mappingWeight: number; }
export interface GradingComponentDefinition { componentId: string; name: string; type: string; weight: number; maxScore: number; isRequired: boolean; minimumScore?: number; isFinalCondition: boolean; cloMappings: CloMapping[]; }
export interface GradingScheme { version: number; academicYear: string; components: GradingComponentDefinition[]; passingScore: number; roundingMode: string; decimalPlaces: number; effectiveFrom: string; active: boolean; }
export interface CourseDesign { courseId: string; courseCode: string; courseName: string; clos: CloDefinition[]; gradingSchemes: GradingScheme[]; }
export interface AdminReport { cards: DashboardCard[]; studentsByFaculty: ChartItem[]; gradeStatus: ChartItem[]; learningStatus: ChartItem[]; cloAchievement: ChartItem[]; recentActivities: { title: string; description: string; time: string; icon: string }[]; }

export interface SemesterOption {
  key: string;
  label: string;
  allCoursesGraded: boolean;
}

export interface SemesterCourseAverage {
  courseCode: string;
  courseName: string;
  credits: number;
  finalScore10: number;
  excludeFromGpa: boolean;
}

export interface SemesterAverageChart {
  semesterKey: string;
  semesterLabel: string;
  allCoursesGraded: boolean;
  average10?: number;
  courses: SemesterCourseAverage[];
}

export interface CurriculumCourse {
  order: number;
  courseCode: string;
  courseName: string;
  credits: number;
  theoryPeriods: number;
  practicePeriods: number;
  group: 'Required' | 'Elective';
  electiveGroup: number;
  requiredCreditsInGroup: number;
  excludeFromGpa: boolean;
  isCoreCourse: boolean;
  isDefaultSelection: boolean;
  isSelected: boolean;
  status: 'Passed' | 'Failed' | 'InProgress' | 'NotRegistered';
  finalScore?: number;
}

export interface CurriculumSemester {
  semesterNumber: number;
  requiredCredits: number;
  electiveCredits: number;
  courses: CurriculumCourse[];
}

export interface StudentCurriculum {
  programCode: string;
  programName: string;
  facultyName: string;
  educationLevel: string;
  applicableCohort: string;
  curriculumVersion: string;
  requiredCredits: number;
  requiredCompulsoryCredits: number;
  requiredElectiveCredits: number;
  completedCredits: number;
  progressPercent: number;
  semesters: CurriculumSemester[];
}
