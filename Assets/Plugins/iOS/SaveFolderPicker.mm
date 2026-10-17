// SaveFolderPicker.mm
// Remake-only: the save folder option on iOS (Data.SaveFolder / SaveFolderPicker). A folder outside the app's sandbox
// (iCloud Drive, On My iPhone, a sync app's provider) comes from the document picker in folder mode; its security-scoped
// URL is kept as a bookmark (the target the game remembers, base64) and opened again at every launch, after which the
// game reads and writes the folder's files with plain file calls while the access is held. The game polls the picker:
// GoF2SaveFolder_State() 0 waiting, 1 picked (Result = the bookmark, Name = the folder's name), 2 cancelled, 3 failed.

#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

// The view the picker is shown on: the key window's root (Unity's own view controller).
static UIViewController* gof2Host()
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

static volatile int gof2State = 0;
static NSString* gof2Result = nil;
static NSString* gof2Name = nil;
static NSString* gof2Error = nil;
static NSURL* gof2Open = nil;   // the folder whose security-scoped access is held

static char* gof2Copy(NSString* s)
{
    if (s == nil) return NULL;
    const char* utf8 = [s UTF8String];
    char* copy = (char*)malloc(strlen(utf8) + 1);
    strcpy(copy, utf8);
    return copy;   // freed by the IL2CPP marshaller (a string return)
}

@interface GoF2SaveFolderDelegate : NSObject <UIDocumentPickerDelegate>
@end

@implementation GoF2SaveFolderDelegate
- (void)documentPicker:(UIDocumentPickerViewController*)controller didPickDocumentsAtURLs:(NSArray<NSURL*>*)urls
{
    NSURL* url = urls.firstObject;
    if (url == nil) { gof2State = 2; return; }
    BOOL access = [url startAccessingSecurityScopedResource];
    NSError* error = nil;
    NSData* bookmark = [url bookmarkDataWithOptions:0 includingResourceValuesForKeys:nil relativeToURL:nil error:&error];
    if (access) [url stopAccessingSecurityScopedResource];
    if (bookmark == nil)
    {
        gof2Error = error != nil ? error.localizedDescription : @"the folder can't be remembered";
        gof2State = 3;
        return;
    }
    gof2Result = [bookmark base64EncodedStringWithOptions:0];
    gof2Name = url.lastPathComponent;
    gof2State = 1;
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController*)controller
{
    gof2State = 2;
}
@end

static GoF2SaveFolderDelegate* gof2Delegate = nil;

extern "C"
{
    void GoF2SaveFolder_Pick()
    {
        gof2State = 0;
        gof2Result = gof2Name = gof2Error = nil;
        dispatch_async(dispatch_get_main_queue(), ^{
            UIDocumentPickerViewController* picker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:@[UTTypeFolder]];
            if (gof2Delegate == nil) gof2Delegate = [[GoF2SaveFolderDelegate alloc] init];
            picker.delegate = gof2Delegate;
            picker.allowsMultipleSelection = NO;
            UIViewController* host = gof2Host();
            if (host == nil) { gof2Error = @"no view to show the picker on"; gof2State = 3; return; }
            [host presentViewController:picker animated:YES completion:nil];
        });
    }

    int GoF2SaveFolder_State() { return gof2State; }
    char* GoF2SaveFolder_Result() { return gof2Copy(gof2Result); }
    char* GoF2SaveFolder_Name() { return gof2Copy(gof2Name); }
    char* GoF2SaveFolder_Error() { return gof2Copy(gof2Error); }

    /// The bookmark's folder opened (its access held until another one is opened): its path, or NULL.
    char* GoF2SaveFolder_Open(const char* bookmarkBase64)
    {
        if (bookmarkBase64 == NULL) return NULL;
        NSData* data = [[NSData alloc] initWithBase64EncodedString:[NSString stringWithUTF8String:bookmarkBase64] options:0];
        if (data == nil) return NULL;
        BOOL stale = NO;
        NSError* error = nil;
        NSURL* url = [NSURL URLByResolvingBookmarkData:data options:0 relativeToURL:nil bookmarkDataIsStale:&stale error:&error];
        if (url == nil) return NULL;
        if (gof2Open != nil && ![gof2Open isEqual:url]) { [gof2Open stopAccessingSecurityScopedResource]; gof2Open = nil; }
        if (gof2Open == nil)
        {
            if (![url startAccessingSecurityScopedResource]) return NULL;
            gof2Open = url;
        }
        return gof2Copy(url.path);
    }

    void GoF2SaveFolder_Close()
    {
        if (gof2Open != nil) { [gof2Open stopAccessingSecurityScopedResource]; gof2Open = nil; }
    }
}
