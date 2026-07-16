const lms = db.getSiblingDB(process.env.MONGO_INITDB_DATABASE || 'EduManageLms');
function ensure(name, validator) {
  if (!lms.getCollectionNames().includes(name)) lms.createCollection(name, { validator, validationLevel: 'moderate', validationAction: 'error' });
  else lms.runCommand({ collMod: name, validator, validationLevel: 'moderate', validationAction: 'error' });
}
ensure('users', { $jsonSchema: { bsonType: 'object', required: ['username','email','passwordHash','role','status'], properties: {
  username: { bsonType:'string', minLength:3 }, email:{bsonType:'string'}, passwordHash:{bsonType:'string'},
  role:{enum:['Admin','Lecturer','Student']}, status:{enum:['Active','Locked','Inactive']}, refreshTokens:{bsonType:'array'}
}}});
ensure('students', { $jsonSchema: { bsonType:'object', required:['studentCode','fullName','faculty','program','academicRecords'], properties:{
  studentCode:{bsonType:'string'}, fullName:{bsonType:'string'}, academicRecords:{bsonType:'array', items:{bsonType:'object', required:['academicYearId','semesters']}}
}}});
ensure('courses', { $jsonSchema: { bsonType:'object', required:['courseCode','courseName','credits','clos','gradingSchemes'], properties:{
  credits:{bsonType:['int','long'],minimum:1}, clos:{bsonType:'array'}, gradingSchemes:{bsonType:'array'}
}}});
ensure('classSections', { $jsonSchema: { bsonType:'object', required:['classSectionCode','courseId','lecturerId','students','gradingSchemeSnapshot','gradeStatus'], properties:{
  gradeStatus:{enum:['Draft','InProgress','Submitted','Published','Locked','Reopened']}, students:{bsonType:'array'}
}}});
print('JSON Schema validators applied');
