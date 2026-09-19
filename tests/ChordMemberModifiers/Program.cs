using Wds.Resone.Resonator;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

var profile = new ResonatorProfile { Ppq = 480, DefaultVelocity = 96 };
var parser = new ResonatorParser(profile);
var result = parser.Parse("[C4 E4@+1/4q G4,:50%:v70] C5");
var notes = result.Composition.Events.OfType<NoteEvent>().OrderBy(n => n.Tick).ThenBy(n => n.MidiNote).ToArray();
Check(result.Warnings.Count == 0, "Unexpected parser warning.");
Check(result.Composition.NominalLengthTicks == 960, "Member timing must not advance the outer chord cursor.");
Check(notes.Length == 4, "Expected three chord members plus the following note.");
Check(notes.Single(n => n.MidiNote == 60).Tick == 0, "C4 should remain on the chord grid point.");
Check(notes.Single(n => n.MidiNote == 64).Tick == 120, "E4 +1/4q onset shift should equal 120 ticks.");
var g = notes.Single(n => n.MidiNote == 67);
Check(g.Tick == 0 && g.DurationTicks == 120 && g.Velocity == 70, "G4 member duration/gate/velocity overrides were not applied independently.");
Check(notes.Single(n => n.MidiNote == 72).Tick == 480, "Following event should remain on the next outer chord grid point.");

profile.Percussion.NoteMap["C2"] = 36;
profile.Percussion.NoteMap["F#2"] = 42;
profile.Percussion.NoteMap["D2"] = 38;
parser = new ResonatorParser(profile);
result = parser.Parse("mode=drums [C2 F#2@+1/8q D2@+1/4q] C2");
notes = result.Composition.Events.OfType<NoteEvent>().OrderBy(n => n.Tick).ThenBy(n => n.MidiNote).ToArray();
Check(notes.Single(n => n.MidiNote == 42).Tick == 60, "Hat +1/8q should equal 60 ticks.");
Check(notes.Single(n => n.MidiNote == 38).Tick == 120, "Snare +1/4q should equal 120 ticks.");
Check(notes.Count(n => n.MidiNote == 36 && n.Tick == 480) == 1, "Next kick should still land on the next quarter-note grid point.");

parser = new ResonatorParser(new ResonatorProfile { Ppq = 480 });
result = parser.Parse("[C4+30c E4~F4]");
notes = result.Composition.Events.OfType<NoteEvent>().ToArray();
Check(notes.Any(n => Math.Abs(n.Cents - 30) < 0.001), "Per-member cents shift missing.");
Check(notes.Any(n => n.BendTargetCents.HasValue && Math.Abs(n.BendTargetCents.Value - 100) < 0.001), "Per-member pitch bend missing.");

Console.WriteLine("Chord member modifier checks passed.");
