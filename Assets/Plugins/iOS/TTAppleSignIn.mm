// Sign in with Apple for Tuition Takkar (called from AppleSignIn.cs). Shows Apple's own sheet and
// hands the identity token back to the game, which gives it to Unity Authentication.
#import <UIKit/UIKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

extern UIViewController* UnityGetGLViewController();

// status: 1 = signed in, 2 = cancelled by the player, 0 = failed
typedef void (*TTAppleCallback)(int status, const char* token, const char* name, const char* error);

API_AVAILABLE(ios(13.0))
@interface TTAppleDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding>
@property (nonatomic, assign) TTAppleCallback callback;
@property (nonatomic, strong) ASAuthorizationController* controller;
@end

static id gAppleDelegate = nil; // kept alive while the sheet is open

@implementation TTAppleDelegate
- (void)finish:(int)status token:(NSString*)token name:(NSString*)name error:(NSString*)error
{
    TTAppleCallback cb = self.callback;
    gAppleDelegate = nil;
    if (cb) cb(status, (token ?: @"").UTF8String, (name ?: @"").UTF8String, (error ?: @"").UTF8String);
}

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithAuthorization:(ASAuthorization*)authorization
{
    if (![authorization.credential isKindOfClass:[ASAuthorizationAppleIDCredential class]])
    {
        [self finish:0 token:nil name:nil error:@"unexpected sign-in result"];
        return;
    }
    ASAuthorizationAppleIDCredential* cred = (ASAuthorizationAppleIDCredential*)authorization.credential;
    NSString* token = cred.identityToken ? [[NSString alloc] initWithData:cred.identityToken encoding:NSUTF8StringEncoding] : nil;
    // Apple only sends the name the very first time a player signs in.
    NSString* name = cred.fullName.givenName ?: @"";
    if (token.length == 0) { [self finish:0 token:nil name:nil error:@"Apple didn't return a sign-in token"]; return; }
    [self finish:1 token:token name:name error:nil];
}

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithError:(NSError*)error
{
    int status = error.code == ASAuthorizationErrorCanceled ? 2 : 0;
    [self finish:status token:nil name:nil error:error.localizedDescription];
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController*)controller
{
    return UnityGetGLViewController().view.window;
}
@end

extern "C" int TT_AppleSupported()
{
    if (@available(iOS 13.0, *)) return 1;
    return 0;
}

extern "C" void TT_AppleSignIn(TTAppleCallback callback)
{
    if (@available(iOS 13.0, *))
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            ASAuthorizationAppleIDProvider* provider = [[ASAuthorizationAppleIDProvider alloc] init];
            ASAuthorizationAppleIDRequest* request = [provider createRequest];
            request.requestedScopes = @[ASAuthorizationScopeFullName];
            TTAppleDelegate* d = [[TTAppleDelegate alloc] init];
            d.callback = callback;
            d.controller = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
            d.controller.delegate = d;
            d.controller.presentationContextProvider = d;
            gAppleDelegate = d;
            [d.controller performRequests];
        });
    }
    else if (callback) callback(0, "", "", "Sign in with Apple needs iOS 13 or newer");
}
