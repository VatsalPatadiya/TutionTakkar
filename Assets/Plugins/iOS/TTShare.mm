// Share sheet for Tuition Takkar room invites (called from NativeShare.cs).
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController();

extern "C" void TT_Share(const char* text)
{
    NSString* message = text ? [NSString stringWithUTF8String:text] : @"";
    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        if (root == nil) return;
        UIActivityViewController* sheet =
            [[UIActivityViewController alloc] initWithActivityItems:@[message] applicationActivities:nil];
        // iPad shows the sheet as a popover, which needs an anchor.
        UIPopoverPresentationController* pop = sheet.popoverPresentationController;
        if (pop != nil)
        {
            pop.sourceView = root.view;
            pop.sourceRect = CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMaxY(root.view.bounds) - 80, 1, 1);
            pop.permittedArrowDirections = UIPopoverArrowDirectionDown;
        }
        [root presentViewController:sheet animated:YES completion:nil];
    });
}
