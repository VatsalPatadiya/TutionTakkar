// Google sign-in on iPhone for Tuition Takkar (called from GoogleSignIn.cs).
// No Google SDK: Apple's own secure sign-in browser (ASWebAuthenticationSession) opens Google's page,
// Google sends the sign-in token to https://tuitiontakkar.web.app/auth/google, and that page hands it
// back to the app as tuitiontakkar://google?id_token=... The token is for the same Google web client as
// Android, so Unity Authentication accepts it the same way.
#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

extern UIViewController* UnityGetGLViewController();

// status: 1 = signed in, 2 = cancelled by the player, 0 = failed
typedef void (*TTGoogleCallback)(int status, const char* token, const char* error);

API_AVAILABLE(ios(13.0))
@interface TTGoogleAnchor : NSObject <ASWebAuthenticationPresentationContextProviding>
@end

@implementation TTGoogleAnchor
- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession*)session
{
    return UnityGetGLViewController().view.window;
}
@end

static ASWebAuthenticationSession* gGoogleSession = nil; // kept alive while the page is open
static id gGoogleAnchor = nil;

static NSString* TTQueryValue(NSURL* url, NSString* name)
{
    NSURLComponents* c = [NSURLComponents componentsWithURL:url resolvingAgainstBaseURL:NO];
    for (NSURLQueryItem* item in c.queryItems)
        if ([item.name isEqualToString:name]) return item.value;
    return nil;
}

extern "C" void TT_GoogleSignIn(const char* authUrl, TTGoogleCallback callback)
{
    NSString* urlString = [NSString stringWithUTF8String:authUrl ?: ""];
    dispatch_async(dispatch_get_main_queue(), ^{
        NSURL* url = [NSURL URLWithString:urlString];
        if (url == nil) { if (callback) callback(0, "", "bad sign-in address"); return; }
        if (gGoogleSession != nil) { [gGoogleSession cancel]; gGoogleSession = nil; }

        ASWebAuthenticationSession* session = [[ASWebAuthenticationSession alloc] initWithURL:url callbackURLScheme:@"tuitiontakkar"
            completionHandler:^(NSURL* callbackURL, NSError* error) {
                gGoogleSession = nil;
                if (error != nil)
                {
                    int status = error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin ? 2 : 0;
                    if (callback) callback(status, "", error.localizedDescription.UTF8String ?: "");
                    return;
                }
                NSString* token = TTQueryValue(callbackURL, @"id_token");
                NSString* err = TTQueryValue(callbackURL, @"error");
                if (token.length > 0) { if (callback) callback(1, token.UTF8String, ""); return; }
                int status = [err isEqualToString:@"access_denied"] ? 2 : 0;
                if (callback) callback(status, "", (err.length > 0 ? err : @"Google didn't return a sign-in token").UTF8String);
            }];
        if (@available(iOS 13.0, *))
        {
            gGoogleAnchor = [[TTGoogleAnchor alloc] init];
            session.presentationContextProvider = gGoogleAnchor;
            session.prefersEphemeralWebBrowserSession = NO; // a Google account already signed in on Safari can be picked
        }
        gGoogleSession = session;
        if (![session start])
        {
            gGoogleSession = nil;
            if (callback) callback(0, "", "couldn't open the Google sign-in page");
        }
    });
}
