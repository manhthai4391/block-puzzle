using System;

public enum PlayMode
{
    Random,
    LevelSequence
}

public enum SequenceEndBehavior
{
    Loop,      // Restart from beginning when sequence ends
    Random,    // Switch to random spawning after sequence completes
    Stop       // Stop spawning new blocks (game will eventually end)
}