const lms=db.getSiblingDB('EduManageLms');
print('Explain student lookup by unique index');
printjson(lms.students.find({studentCode:'SV001'}).explain('executionStats'));
print('Explain class sections by lecturer/year/semester compound index');
const lecturer=lms.lecturers.findOne({lecturerCode:'GV001'});
const section=lms.classSections.findOne({lecturerId:lecturer?._id});
if(section) printjson(lms.classSections.find({academicYearId:section.academicYearId,semesterId:section.semesterId,lecturerId:section.lecturerId}).explain('executionStats'));
