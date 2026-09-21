using Wds.Resone.Api;
foreach(var item in new[] {(48,"C2"),(60,"C3"),(66,"F#3"),(0,"C-2"),(127,"G8")})
    if(NoteNameContext.PitchName(item.Item1)!=item.Item2) throw new Exception("Pitch conversion failed");
var original=new Note(1.5, .5, 48, 90);
var result=NoteNameContext.Create(new[]{original});
if(result[0]!["pitch"]!.GetValue<string>()!="C2" || result[0]!["start"]!.GetValue<double>()!=1.5
    || result[0]!["duration"]!.GetValue<double>()!=.5 || result[0]!["velocity"]!.GetValue<int>()!=90
    || original.Pitch!=48) throw new Exception("Timeline or playback data changed");
Console.WriteLine("Note-name context checks passed");
namespace Wds.Resone.Api { public sealed record Note(double Start,double Duration,int Pitch,int Velocity=96); }
