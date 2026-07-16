const lms = db.getSiblingDB('EduManageLms');
const studentCode = 'SV001';

// 1. Điểm tổng kết môn + điểm chữ + hệ 4.
const courseGradePipeline = [
 {$match:{studentCode,isDeleted:false}},
 {$unwind:'$academicRecords'}, {$unwind:'$academicRecords.semesters'}, {$unwind:'$academicRecords.semesters.courses'},
 {$unwind:'$academicRecords.semesters.courses.scores'},
 {$addFields:{componentWeighted:{$multiply:[{$divide:[{$ifNull:['$academicRecords.semesters.courses.scores.score',0]},'$academicRecords.semesters.courses.scores.maxScore']},10,{$divide:['$academicRecords.semesters.courses.scores.weight',100]}]}}},
 {$group:{_id:{studentCode:'$studentCode',courseCode:'$academicRecords.semesters.courses.courseCode',classSectionId:'$academicRecords.semesters.courses.classSectionId'},course:{$first:'$academicRecords.semesters.courses'},rawFinal:{$sum:'$componentWeighted'}}},
 {$addFields:{finalScore:{$round:['$rawFinal',2]},grade:{$switch:{branches:[
   {case:{$gte:['$rawFinal',8.5]},then:{letter:'A',point:4.0}}, {case:{$gte:['$rawFinal',8.0]},then:{letter:'B+',point:3.5}},
   {case:{$gte:['$rawFinal',7.0]},then:{letter:'B',point:3.0}}, {case:{$gte:['$rawFinal',6.5]},then:{letter:'C+',point:2.5}},
   {case:{$gte:['$rawFinal',5.5]},then:{letter:'C',point:2.0}}, {case:{$gte:['$rawFinal',5.0]},then:{letter:'D+',point:1.5}},
   {case:{$gte:['$rawFinal',4.0]},then:{letter:'D',point:1.0}}],default:{letter:'F',point:0}}}}},
 {$project:{_id:0,studentCode:'$_id.studentCode',courseCode:'$_id.courseCode',courseName:'$course.courseName',credits:'$course.credits',finalScore:1,letterGrade:'$grade.letter',gradePoint:'$grade.point',passed:{$gte:['$finalScore',4]}}},
 {$sort:{courseCode:1}}
];
print('COURSE GRADES'); printjson(lms.students.aggregate(courseGradePipeline).toArray());

// 2. GPA học kỳ theo tín chỉ. Không dùng vòng lặp backend.
const semesterGpaPipeline = [
 ...courseGradePipeline.slice(0,-2),
 {$group:{_id:{studentCode:'$studentCode',year:'$academicRecords.academicYearName',semester:'$academicRecords.semesters.semesterCode'},weightedPoints:{$sum:{$multiply:['$grade.point','$course.credits']}},weightedScore10:{$sum:{$multiply:['$finalScore','$course.credits']}},totalCredits:{$sum:'$course.credits'},passedCredits:{$sum:{$cond:[{$gte:['$finalScore',4]},'$course.credits',0]}}}},
 {$project:{_id:0,studentCode:'$_id.studentCode',academicYear:'$_id.year',semester:'$_id.semester',gpa:{$round:[{$divide:['$weightedPoints','$totalCredits']},2]},average10:{$round:[{$divide:['$weightedScore10','$totalCredits']},2]},totalCredits:1,passedCredits:1}}
];

// 3. CLO cá nhân và tỷ lệ đạt lớp.
const cloPipeline = [
 {$match:{studentCode,isDeleted:false}},{$unwind:'$academicRecords'},{$unwind:'$academicRecords.semesters'},{$unwind:'$academicRecords.semesters.courses'},{$unwind:'$academicRecords.semesters.courses.scores'},{$unwind:'$academicRecords.semesters.courses.scores.cloMappings'},
 {$addFields:{normalized:{$divide:[{$ifNull:['$academicRecords.semesters.courses.scores.score',0]},'$academicRecords.semesters.courses.scores.maxScore']},validWeight:{$multiply:['$academicRecords.semesters.courses.scores.weight','$academicRecords.semesters.courses.scores.cloMappings.mappingWeight']}}},
 {$group:{_id:{studentCode:'$studentCode',courseCode:'$academicRecords.semesters.courses.courseCode',cloCode:'$academicRecords.semesters.courses.scores.cloMappings.cloCode'},weighted:{$sum:{$multiply:['$normalized','$validWeight']}},totalWeight:{$sum:'$validWeight'}}},
 {$addFields:{percentage:{$round:[{$multiply:[{$divide:['$weighted','$totalWeight']},100]},2]}}},
 {$project:{_id:0,studentCode:'$_id.studentCode',courseCode:'$_id.courseCode',cloCode:'$_id.cloCode',percentage:1,threshold:{$literal:50},passed:{$gte:['$percentage',50]}}}
];

// 4. Phân bố học lực dùng $bucket.
const distributionPipeline = [...courseGradePipeline.slice(0,-2),{$bucket:{groupBy:'$finalScore',boundaries:[0,4,5.5,7,8.5,10.01],default:'Unknown',output:{count:{$sum:1},students:{$addToSet:'$_id.studentCode'}}}}];
