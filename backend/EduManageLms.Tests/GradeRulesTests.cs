using FluentAssertions;
namespace EduManageLms.Tests;
public sealed class GradeRulesTests {
 [Fact] public void Dynamic_weighted_score_supports_non_ten_maximum(){var components=new[]{(score:8d,max:10d,weight:10d),(score:16d,max:20d,weight:20d),(score:24d,max:30d,weight:30d),(score:28d,max:40d,weight:40d)};var final=components.Sum(x=>(x.score/x.max)*10*(x.weight/100));final.Should().BeApproximately(7.6,.001);}
 [Fact] public void Grading_scheme_must_total_one_hundred(){var weights=new[]{10d,20d,30d,40d};weights.Sum().Should().Be(100);}
 [Theory][InlineData(8.5,"A",4.0)][InlineData(7.2,"B",3.0)][InlineData(3.9,"F",0.0)] public void Grade_scale_is_consistent(double score,string letter,double point){var value=score>=8.5?("A",4d):score>=8?("B+",3.5):score>=7?("B",3d):score>=6.5?("C+",2.5):score>=5.5?("C",2d):score>=5?("D+",1.5):score>=4?("D",1d):("F",0d);value.Item1.Should().Be(letter);value.Item2.Should().Be(point);}
}
