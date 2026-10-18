// FileImportPicker.mm
// Remake-only: the document picker for Modding.ModImport on iOS (the mod browser's Import mod, the Mod Builder's file
// pickers, the original game's save import), like Android's ModImportActivity: the picked file (iCloud Drive, On My
// iPhone, any file provider) is copied by the system ("asCopy") and moved into the folder the game names as "picked.tmp".
// The game polls it: GoF2FilePick_State() 0 waiting, 1 picked (Result = the copy's path, Name = the file's own name),
// 2 cancelled, 3 failed (Error).

#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

static UIViewController* gof2PickHost()
{
    for (UIScene* scene in [UIApplication sharedApplication].connectedScenes)
    {
        if (![scene isKindOfClass:[UIWindowScene class]]) continue;
        for (UIWindow* window in ((UIWindowScene*)scene).windows)
            if (window.isKeyWindow && window.rootViewController != nil)
            {
                UIViewController* vc = window.rootViewController;
                while (vc.presentedViewController != nil) vc = vc.presentedViewController;
                return vc;
            }
    }
    return nil;
}

static volatile int gof2PickState = 0;
static NSString* gof2PickResult = nil;
static NSString* gof2PickName = nil;
static NSString* gof2PickError = nil;
static NSString* gof2PickFolder = nil;

static char* gof2PickCopy(NSString* s)
{
    if (s == nil) return NULL;
    const char* utf8 = [s UTF8String];
    char* copy = (char*)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy;   // freed by the IL2CPP marshaller (a string return)
}

@interface GoF2FilePickDelegate : NSObject <UIDocumentPickerDelegate, UIAdaptivePresentationControllerDelegate>
@end

@implementation GoF2FilePickDelegate
- (void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    NSURL* url = urls.firstObject;
    if (url == nil) { gof2PickState = 2; return; }
    NSFileManager* files = [NSFileManager defaultManager];
    NSError* error = nil;
    [files createDirectoryAtPath:gof2PickFolder withIntermediateDirectories:YES attributes:nil error:nil];
    NSString* target = [gof2PickFolder stringByAppendingPathComponent:@"picked.tmp"];
    [files removeItemAtPath:target error:nil];
    BOOL access = [url startAccessingSecurityScopedResource];
    // "asCopy" already made a copy in the app's tmp/<bundle id>-Inbox: moved, not copied again (a mod zip of 120 MB stayed
    // there twice); a file outside the app's own folders is copied.
    BOOL copied = [url.path hasPrefix:NSTemporaryDirectory()]
        && [files moveItemAtPath:url.path toPath:target error:nil];
    if (!copied) copied = [files copyItemAtPath:url.path toPath:target error:&error];
    if (access) [url stopAccessingSecurityScopedResource];
    if (!copied)
    {
        gof2PickError = error != nil ? error.localizedDescription : @"the file couldn't be copied";
        gof2PickState = 3;
        return;
    }
    gof2PickResult = target;
    gof2PickName = url.lastPathComponent;
    gof2PickState = 1;
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller
{
    gof2PickState = 2;
}

// The sheet swiped down: documentPickerWasCancelled isn't always called then, and the game would wait for good.
- (void)presentationControllerDidDismiss:(UIPresentationController*)presentationController
{
    if (gof2PickState == 0) gof2PickState = 2;
}
@end

static GoF2FilePickDelegate* gof2PickDelegate = nil;

extern "C"
{
    /// Opens the picker for the comma-separated extensions (without the dot); every file when one isn't a known type.
    void GoF2FilePick_Start(const char* folder, const char* extensions)
    {
        gof2PickState = 0;
        gof2PickResult = gof2PickName = gof2PickError = nil;
        gof2PickFolder = folder != NULL ? [NSString stringWithUTF8String:folder] : NSTemporaryDirectory();
        NSMutableArray<UTType*>* types = [NSMutableArray array];
        BOOL any = extensions == NULL;
        if (extensions != NULL)
            for (NSString* ext in [[NSString stringWithUTF8String:extensions] componentsSeparatedByString:@","])
            {
                NSString* e = [ext stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];
                if (e.length == 0 || [e isEqualToString:@"*"]) { any = YES; continue; }
                UTType* t = [UTType typeWithFilenameExtension:e];
                // A dynamic type (an extension iOS doesn't know: .glb, .aei, .gof2event) matches no file in the picker.
                if (t == nil || t.isDynamic) any = YES; else [types addObject:t];
            }
        if (any || types.count == 0) types = [NSMutableArray arrayWithObject:UTTypeItem];
        NSArray<UTType*>* picked = [types copy];
        dispatch_async(dispatch_get_main_queue(), ^{
            UIDocumentPickerViewController* picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:picked asCopy:YES];
            if (gof2PickDelegate == nil) gof2PickDelegate = [[GoF2FilePickDelegate alloc] init];
            picker.delegate = gof2PickDelegate;
            picker.allowsMultipleSelection = NO;
            picker.presentationController.delegate = gof2PickDelegate;   // a swipe-down counts as a cancel
            UIViewController* host = gof2PickHost();
            if (host == nil) { gof2PickError = @"no view to show the picker on"; gof2PickState = 3; return; }
            [host presentViewController:picker animated:YES completion:nil];
        });
    }

    int GoF2FilePick_State() { return gof2PickState; }
    char* GoF2FilePick_Result() { return gof2PickCopy(gof2PickResult); }
    char* GoF2FilePick_Name() { return gof2PickCopy(gof2PickName); }
    char* GoF2FilePick_Error() { return gof2PickCopy(gof2PickError); }
}
