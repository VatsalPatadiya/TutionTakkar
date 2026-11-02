// iPhone taps for Tuition Takkar (called from Haptics.cs): 0 light, 1 medium, 2 heavy, 3 success.
#import <UIKit/UIKit.h>

extern "C" void TT_Haptic(int level)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (level >= 3)
        {
            UINotificationFeedbackGenerator* g = [[UINotificationFeedbackGenerator alloc] init];
            [g notificationOccurred:UINotificationFeedbackTypeSuccess];
            return;
        }
        UIImpactFeedbackStyle style = level <= 0 ? UIImpactFeedbackStyleLight : level == 1 ? UIImpactFeedbackStyleMedium : UIImpactFeedbackStyleHeavy;
        UIImpactFeedbackGenerator* g = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
        [g impactOccurred];
    });
}
