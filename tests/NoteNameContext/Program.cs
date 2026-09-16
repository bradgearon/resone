using Wds.Resone.Api;
foreach(var item in new[] {(48,"C3"),(60,"C4"),(66,"F#4"),(0,"C-1"),(127,"G9")})
    if(NoteNameContext.PitchName(item.Item1)!=item.Item2) throw new Exception("Pitch conversion failed");
var original=new Note(1.5, .5, 48, 90);
var result=NoteNameContext.Create(new[]{original});
if(result[0]!["pitch"]!.GetValue<string>()!="C3" || result[0]!["start"]!.GetValue<double>()!=1.5
    || result[0]!["duration"]!.GetValue<double>()!=.5 || result[0]!["velocity"]!.GetValue<int>()!=90
    || original.Pitch!=48) throw new Exception("Timeline or playback data changed");
Console.WriteLine("Note-name context checks passed");
namespace Wds.Resone.Api { public sealed record Note(double Start,double Duration,int Pitch,int Velocity=96); }
