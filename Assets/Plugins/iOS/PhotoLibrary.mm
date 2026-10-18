// PhotoLibrary.mm
// Remake-only: a screenshot (Action Freeze, Inspect ship, the screenshot key) into the iPhone's photo library, like Android's
// MediaStore "Pictures/Galaxy on Fire 2" (UI.PhotoMode.StoreAt). The PNG is written to Documents/Screenshots first (the Files
// app shows it, UIFileSharingEnabled); this adds that file to Photos with add-only access (the first time iOS asks, with
// Info.plist's NSPhotoLibraryAddUsageDescription, IosPlayerSetup). Asynchronous: GoF2Photos_State() 0 working, 1 saved,
// 2 refused (no permission), 3 failed.

#import <Foundation/Foundation.h>
#import <Photos/Photos.h>

static volatile int gof2PhotoState = 1;

extern "C"
{
    void GoF2Photos_Save(const char* path)
    {
        if (path == NULL) { gof2PhotoState = 3; return; }
        NSURL* url = [NSURL fileURLWithPath:[NSString stringWithUTF8String:path]];
        gof2PhotoState = 0;
        [PHPhotoLibrary requestAuthorizationForAccessLevel:PHAccessLevelAddOnly handler:^(PHAuthorizationStatus status) {
            if (status != PHAuthorizationStatusAuthorized && status != PHAuthorizationStatusLimited)
            {
                gof2PhotoState = 2;
                return;
            }
            [[PHPhotoLibrary sharedPhotoLibrary] performChanges:^{
                [PHAssetChangeRequest creationRequestForAssetFromImageAtFileURL:url];
            } completionHandler:^(BOOL success, NSError* error) {
                if (!success && error != nil) NSLog(@"GoF2 PhotoLibrary: %@", error.localizedDescription);
                gof2PhotoState = success ? 1 : 3;
            }];
        }];
    }

    int GoF2Photos_State() { return gof2PhotoState; }
}
