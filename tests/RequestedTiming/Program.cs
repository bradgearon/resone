using Wds.Resone.Api;
var cases = new (string Input, string Expected)[] {
    ("tempo=80 3/4 C4 E4", "tempo=120 4/4\nC4 E4"),
    ("96 7/4 | C4, _ |", "tempo=120 4/4\n| C4, _ |"),
    ("C4 E4", "tempo=120 4/4\nC4 E4"),
    ("tempo=0 99/0|C4@+1/8q:85% [E4 G4]:v96 / //", "tempo=120 4/4\n|C4@+1/8q:85% [E4 G4]:v96 / //"),
    ("tempo=120 4/4\nC4, - _.", "tempo=120 4/4\nC4, - _."),
};
foreach (var c in cases) {
    var actual = RequestedTiming.Apply(c.Input,120,"4/4");
    if(actual!=c.Expected) throw new Exception($"Unexpected normalization: {actual}");
    if(RequestedTiming.Apply(actual,120,"4/4")!=actual) throw new Exception("Not idempotent");
}
if(RequestedTiming.Apply("tempo=80 4/4 C4",96,"6/8")!="tempo=96 6/8\nC4") throw new Exception("6/8 override failed");
Console.WriteLine("Timing override checks passed.");
