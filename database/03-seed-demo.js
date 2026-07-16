// Script seed dùng khi chạy mongosh độc lập. Backend cũng có DataSeeder tương đương.
const lms = db.getSiblingDB(process.env.MONGO_INITDB_DATABASE || 'EduManageLms');
if (lms.students.countDocuments() > 0) { print('Seed skipped: data already exists'); quit(); }
const now = new Date();
const faculty = { facultyId:new ObjectId(), facultyCode:'CNTT', facultyName:'Công nghệ thông tin' };
const program = { programId:new ObjectId(), programCode:'CNTT2024', programName:'Công nghệ thông tin', requiredCredits:130 };
const year = { _id:new ObjectId(), code:'2025-2026', name:'Năm học 2025-2026', current:true };
const semester = { _id:new ObjectId(), code:'HK1', name:'Học kỳ 1', academicYearId:year._id };
lms.academicYears.insertOne(year); lms.semesters.insertOne(semester);
const components = [
 {componentId:'CC',name:'Chuyên cần',type:'Attendance',weight:10,maxScore:10,cloMappings:[{cloCode:'CLO1',mappingWeight:100}]},
 {componentId:'BT',name:'Bài tập',type:'Assignment',weight:20,maxScore:10,cloMappings:[{cloCode:'CLO1',mappingWeight:40},{cloCode:'CLO2',mappingWeight:60}]},
 {componentId:'GK',name:'Giữa kỳ',type:'Midterm',weight:30,maxScore:10,cloMappings:[{cloCode:'CLO2',mappingWeight:100}]},
 {componentId:'CK',name:'Cuối kỳ',type:'Final',weight:40,maxScore:10,isRequired:true,minimumScore:3,cloMappings:[{cloCode:'CLO2',mappingWeight:40},{cloCode:'CLO3',mappingWeight:60}]}
];
const courseNames=['Cơ sở dữ liệu NoSQL','Phát triển Web API','Internet vạn vật','Công nghệ phần mềm','Trí tuệ nhân tạo','Mạng máy tính','An toàn thông tin','Lập trình di động','Điện toán đám mây','Kiểm thử phần mềm','Phân tích thiết kế hệ thống','Khai phá dữ liệu','Cấu trúc dữ liệu','Lập trình hướng đối tượng','Cơ sở dữ liệu'];
const courses=courseNames.map((name,i)=>({ _id:new ObjectId(), courseCode:['NOSQL01','WEBAPI','IOT101','SE101','AI101'][i]||`IT${i+1}01`, courseName:name, credits:i%4===0?4:3, faculty, status:'Active',
 clos:[1,2,3].map(n=>({cloCode:`CLO${n}`,name:`Chuẩn đầu ra ${n}`,description:`Năng lực cần đạt ${n}`,bloomLevel:['Understand','Apply','Analyze'][n-1],threshold:50,weight:n===2?40:30,active:true})),
 gradingSchemes:[{version:1,academicYear:'2025-2026',components,passingScore:4,roundingMode:'Normal',decimalPlaces:2,effectiveFrom:now,active:true}], createdAt:now,updatedAt:now,isDeleted:false }));
lms.courses.insertMany(courses);
const lecturers=Array.from({length:10},(_,i)=>({_id:new ObjectId(),lecturerCode:`GV${String(i+1).padStart(3,'0')}`,fullName:`Giảng viên ${String(i+1).padStart(2,'0')}`,email:`gv${String(i+1).padStart(3,'0')}@lms.edu.vn`,faculty,department:i%2?'Khoa học máy tính':'Hệ thống thông tin',degree:'Thạc sĩ',status:'Active',createdAt:now,updatedAt:now,isDeleted:false}));
lms.lecturers.insertMany(lecturers);
const sections=courses.slice(0,12).map((c,i)=>({_id:new ObjectId(),classSectionCode:`${c.courseCode}-0${i%2+1}`,courseId:c._id,courseCode:c.courseCode,courseName:c.courseName,academicYearId:year._id,academicYearName:'2025-2026',semesterId:semester._id,semesterCode:'HK1',semesterName:'Học kỳ 1',lecturerId:lecturers[i%10]._id,lecturerCode:lecturers[i%10].lecturerCode,lecturerName:lecturers[i%10].fullName,capacity:40,students:[],gradingSchemeSnapshot:{version:1,academicYear:'2025-2026',components,passingScore:4,roundingMode:'Normal',decimalPlaces:2},gradeStatus:i<4?'Published':'InProgress',schedule:[{dayOfWeek:i%2?'Wednesday':'Monday',startTime:'07:00',endTime:'09:30',room:`A.${i+1}01`}],startDate:new Date('2026-01-05'),endDate:new Date('2026-05-25'),createdAt:now,updatedAt:now,isDeleted:false}));
const students=[];
for(let i=1;i<=80;i++){
 const studentId=new ObjectId(), code=`SV${String(i).padStart(3,'0')}`; const selected=sections.filter((_,idx)=>idx<4 || i%3===idx%3).slice(0,6);
 const records=selected.map((s,ci)=>{const base=i%13===0?3.2:5.5+((i*17+ci*7)%40)/10;return {courseId:s.courseId,courseCode:s.courseCode,courseName:s.courseName,credits:courses.find(c=>c._id.equals(s.courseId)).credits,classSectionId:s._id,classSectionCode:s.classSectionCode,lecturer:{lecturerId:s.lecturerId,lecturerCode:s.lecturerCode,fullName:s.lecturerName},gradingSchemeVersion:1,scores:components.map((x,k)=>({componentId:x.componentId,componentName:x.name,type:x.type,weight:x.weight,maxScore:x.maxScore,score:(i%19===0&&k===3)?null:Math.max(0,Math.min(10,Math.round((base+(k-1.5)*.3)*10)/10)),status:(i%19===0&&k===3)?'NotGraded':'Graded',isRequired:!!x.isRequired,minimumScore:x.minimumScore||null,cloMappings:x.cloMappings})),attemptNumber:i%17===0?2:1,scoreStatus:s.gradeStatus,publishedAt:s.gradeStatus==='Published'?new Date(now-86400000*3):null};});
 const st={_id:studentId,studentCode:code,fullName:`Sinh viên ${String(i).padStart(3,'0')}`,email:`sv${String(i).padStart(3,'0')}@lms.edu.vn`,faculty,program,cohort:'2024',administrativeClass:i<=40?'14DHTH01':'14DHTH02',status:'Studying',academicRecords:[{academicYearId:year._id,academicYearName:'2025-2026',semesters:[{semesterId:semester._id,semesterCode:'HK1',semesterName:'Học kỳ 1',courses:records}]}],createdAt:now,updatedAt:now,isDeleted:false}; students.push(st); selected.forEach(s=>s.students.push({studentId,studentCode:code,fullName:st.fullName,status:'Enrolled'}));
}
lms.students.insertMany(students); lms.classSections.insertMany(sections);
// PasswordHash bên dưới được backend seed bằng BCrypt. Khi chạy script này, hãy đăng nhập sau khi chạy backend seed hoặc thay hash bằng BCrypt hợp lệ.
lms.notifications.insertMany(Array.from({length:20},(_,i)=>({_id:new ObjectId(),title:`Thông báo học vụ số ${i+1}`,content:'Nội dung thông báo phục vụ demo LMS.',type:'Academic',priority:(i+1)%5===0?'High':'Normal',recipientIds:[],audienceType:i%2?'Student':'All',readBy:[],status:'Sent',displayFrom:now,createdAt:now,updatedAt:now,isDeleted:false})));
print(`Seeded ${students.length} students, ${lecturers.length} lecturers, ${courses.length} courses and ${sections.length} sections`);
