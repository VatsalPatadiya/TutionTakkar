// iPhone notifications for Tuition Takkar (called from Push.cs).
// Unity Mobile Notifications asks permission and gives the Apple (APNs) token; this file hands it to
// Firebase Cloud Messaging, which returns an FCM token, so the same server scripts reach Android and iPhone.
// Firebase comes from Swift Package Manager (added to the Xcode project by AppleXcodeSetup.cs).
#import <Foundation/Foundation.h>
#import <FirebaseCore/FirebaseCore.h>
#import <FirebaseMessaging/FirebaseMessaging.h>

@interface TTPushDelegate : NSObject <FIRMessagingDelegate>
@end

static NSString* gFcmToken = @"";
static TTPushDelegate* gPushDelegate = nil;

@implementation TTPushDelegate
- (void)messaging:(FIRMessaging*)messaging didReceiveRegistrationToken:(NSString*)fcmToken
{
    if (fcmToken.length == 0) return;
    gFcmToken = [fcmToken copy];
    // Same as Android: every phone joins "all", so a Firebase campaign to topic "all" reaches everyone.
    [messaging subscribeToTopic:@"all" completion:nil];
}
@end

static void TTPushEnsure()
{
    if ([FIRApp defaultApp] == nil) [FIRApp configure];
    if (gPushDelegate == nil)
    {
        gPushDelegate = [[TTPushDelegate alloc] init];
        [FIRMessaging messaging].delegate = gPushDelegate;
    }
}

extern "C" void TT_PushInit()
{
    dispatch_async(dispatch_get_main_queue(), ^{ TTPushEnsure(); });
}

extern "C" void TT_PushSetApns(const char* hex)
{
    if (hex == NULL) return;
    NSString* s = [[NSString stringWithUTF8String:hex] stringByReplacingOccurrencesOfString:@" " withString:@""];
    NSMutableData* data = [NSMutableData dataWithCapacity:s.length / 2];
    for (NSUInteger i = 0; i + 1 < s.length; i += 2)
    {
        unsigned int byte = 0;
        [[NSScanner scannerWithString:[s substringWithRange:NSMakeRange(i, 2)]] scanHexInt:&byte];
        uint8_t b = (uint8_t)byte;
        [data appendBytes:&b length:1];
    }
    dispatch_async(dispatch_get_main_queue(), ^{
        TTPushEnsure();
        [FIRMessaging messaging].APNSToken = data;
        [[FIRMessaging messaging] tokenWithCompletion:^(NSString* token, NSError* error) {
            if (token.length > 0) gFcmToken = [token copy];
        }];
    });
}

// Unity frees the returned string, so it must be malloc'ed.
extern "C" char* TT_PushToken()
{
    const char* s = (gFcmToken ?: @"").UTF8String;
    char* out = (char*)malloc(strlen(s) + 1);
    strcpy(out, s);
    return out;
}
