// iPhone notifications for Tuition Takkar (called from Push.cs).
// Unity Mobile Notifications asks permission and gives the Apple (APNs) token; this file hands it to
// Firebase Cloud Messaging, which returns an FCM token, so the same server scripts reach Android and iPhone.
//
// Firebase is linked into the main app (Swift Package Manager, added by AppleXcodeSetup.cs), not into
// UnityFramework, so this file talks to it at run time (NSClassFromString) and needs no Firebase headers.
#import <Foundation/Foundation.h>

static NSString* gFcmToken = @"";

@interface TTPushDelegate : NSObject
@end

@implementation TTPushDelegate
// FIRMessagingDelegate: called when Firebase has (or refreshes) this phone's messaging token.
- (void)messaging:(id)messaging didReceiveRegistrationToken:(NSString*)fcmToken
{
    if (![fcmToken isKindOfClass:[NSString class]] || fcmToken.length == 0) return;
    gFcmToken = [fcmToken copy];
    // Same as Android: every phone joins "all", so a Firebase campaign to topic "all" reaches everyone.
    SEL sub = NSSelectorFromString(@"subscribeToTopic:completion:");
    if ([messaging respondsToSelector:sub])
        ((void (*)(id, SEL, NSString*, id))[messaging methodForSelector:sub])(messaging, sub, @"all", nil);
}
@end

static TTPushDelegate* gPushDelegate = nil;

static id TTMessaging()
{
    Class app = NSClassFromString(@"FIRApp");
    Class msg = NSClassFromString(@"FIRMessaging");
    if (app == nil || msg == nil) { NSLog(@"[TuitionTakkar] Firebase not linked: iPhone notifications off"); return nil; }
    SEL defaultApp = NSSelectorFromString(@"defaultApp");
    SEL configure = NSSelectorFromString(@"configure");
    id existing = ((id (*)(id, SEL))[app methodForSelector:defaultApp])(app, defaultApp);
    if (existing == nil) ((void (*)(id, SEL))[app methodForSelector:configure])(app, configure);
    SEL messaging = NSSelectorFromString(@"messaging");
    id m = ((id (*)(id, SEL))[msg methodForSelector:messaging])(msg, messaging);
    if (m != nil && gPushDelegate == nil)
    {
        gPushDelegate = [[TTPushDelegate alloc] init];
        SEL setDelegate = NSSelectorFromString(@"setDelegate:");
        ((void (*)(id, SEL, id))[m methodForSelector:setDelegate])(m, setDelegate, gPushDelegate);
    }
    return m;
}

extern "C" void TT_PushInit()
{
    dispatch_async(dispatch_get_main_queue(), ^{ TTMessaging(); });
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
        id m = TTMessaging();
        if (m == nil) return;
        SEL setApns = NSSelectorFromString(@"setAPNSToken:");
        ((void (*)(id, SEL, NSData*))[m methodForSelector:setApns])(m, setApns, data);
        SEL tokenWith = NSSelectorFromString(@"tokenWithCompletion:");
        if ([m respondsToSelector:tokenWith])
        {
            void (^done)(NSString*, NSError*) = ^(NSString* token, NSError* error) {
                if (token.length > 0) gFcmToken = [token copy];
            };
            ((void (*)(id, SEL, id))[m methodForSelector:tokenWith])(m, tokenWith, done);
        }
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
