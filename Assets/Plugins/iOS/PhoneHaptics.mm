// PhoneHaptics.mm
// Remake-only: the iPhone's Taptic Engine for Haptics (Flight.PhoneVibrator's iOS side) through Core Haptics: a pulse is a
// continuous haptic event of its length and intensity (a transient one under 30 ms), played at once; a new pulse stops the
// one running (PhoneVibrator already drops a weaker one while a stronger one runs). The engine stops by itself while idle
// (auto shutdown) and is started again on the next pulse; a reset (the app back from the background, an audio session
// change) or a stop marks it for a restart. Devices without a Taptic Engine (iPads, the iPod touch) report no support.

#import <Foundation/Foundation.h>
#import <CoreHaptics/CoreHaptics.h>

static CHHapticEngine* gof2Engine = nil;
static id<CHHapticPatternPlayer> gof2Player = nil;
static volatile BOOL gof2Running = NO;
static int gof2Supported = -1;   // unknown

static BOOL gof2EnsureEngine()
{
    if (gof2Engine == nil)
    {
        NSError* error = nil;
        gof2Engine = [[CHHapticEngine alloc] initAndReturnError:&error];
        if (gof2Engine == nil) { gof2Supported = 0; return NO; }
        gof2Engine.playsHapticsOnly = YES;      // no audio: the game's own sounds go through Unity
        gof2Engine.autoShutdownEnabled = YES;   // idle = off, saves power
        // Core Haptics calls these on its own queue; the pulses read and replace gof2Player on the main thread (Unity's),
        // and a strong static assigned from two threads can over-release the player: both handled on the main thread.
        gof2Engine.resetHandler = ^{ dispatch_async(dispatch_get_main_queue(), ^{ gof2Running = NO; gof2Player = nil; }); };
        gof2Engine.stoppedHandler = ^(CHHapticEngineStoppedReason reason) {
            dispatch_async(dispatch_get_main_queue(), ^{ gof2Running = NO; gof2Player = nil; });
        };
    }
    if (!gof2Running)
    {
        NSError* error = nil;
        gof2Running = [gof2Engine startAndReturnError:&error];
    }
    return gof2Running;
}

extern "C"
{
    bool GoF2Haptics_Supported()
    {
        if (gof2Supported < 0)
            gof2Supported = [CHHapticEngine capabilitiesForHardware].supportsHaptics ? 1 : 0;
        return gof2Supported == 1;
    }

    /// A pulse of 'seconds' at 'intensity' 0..1 (the option's strength already applied).
    void GoF2Haptics_Pulse(float seconds, float intensity)
    {
        if (!GoF2Haptics_Supported() || seconds <= 0.0f || intensity <= 0.0f) return;
        if (!gof2EnsureEngine()) return;
        if (intensity > 1.0f) intensity = 1.0f;
        NSArray* parameters = @[
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticIntensity value:intensity],
            // Sharper for the short knocks (hits, locks), rounder for the long rumbles (the drill, the Khador charge).
            [[CHHapticEventParameter alloc] initWithParameterID:CHHapticEventParameterIDHapticSharpness
                                                          value:seconds < 0.06f ? 0.7f : 0.35f],
        ];
        CHHapticEvent* event = seconds < 0.03f
            ? [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticTransient parameters:parameters relativeTime:0]
            : [[CHHapticEvent alloc] initWithEventType:CHHapticEventTypeHapticContinuous parameters:parameters
                                          relativeTime:0 duration:seconds];
        NSError* error = nil;
        CHHapticPattern* pattern = [[CHHapticPattern alloc] initWithEvents:@[event] parameters:@[] error:&error];
        if (pattern == nil) return;
        id<CHHapticPatternPlayer> player = [gof2Engine createPlayerWithPattern:pattern error:&error];
        if (player == nil) { gof2Running = NO; return; }   // the engine went away meanwhile: started again next time
        if (gof2Player != nil) [gof2Player stopAtTime:CHHapticTimeImmediate error:nil];
        if ([player startAtTime:CHHapticTimeImmediate error:&error]) gof2Player = player;
        else { gof2Running = NO; gof2Player = nil; }
    }

    /// Stops the pulse running (pause, focus lost, quitting).
    void GoF2Haptics_Stop()
    {
        if (gof2Player != nil) [gof2Player stopAtTime:CHHapticTimeImmediate error:nil];
        gof2Player = nil;
    }
}
