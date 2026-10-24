package com.unitygame.push;

import android.app.Notification;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;

import com.google.firebase.messaging.FirebaseMessagingService;
import com.google.firebase.messaging.RemoteMessage;

/**
 * When the game is closed or in the background, Firebase shows the notification itself.
 * This only runs while the game is open, so the message still appears in the tray.
 */
public class PushService extends FirebaseMessagingService {
    @Override
    public void onMessageReceived(RemoteMessage msg) {
        RemoteMessage.Notification n = msg.getNotification();
        String title = n != null ? n.getTitle() : msg.getData().get("title");
        String body = n != null ? n.getBody() : msg.getData().get("body");
        if (title == null && body == null) return;

        PushBridge.ensureChannel(this);
        Intent open = getPackageManager().getLaunchIntentForPackage(getPackageName());
        if (open == null) return;
        open.addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP);
        String link = msg.getData().get("link");
        if (link != null) open.putExtra("link", link);
        PendingIntent tap = PendingIntent.getActivity(this, (int) System.currentTimeMillis(), open,
            PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        Notification note = new Notification.Builder(this, PushBridge.CHANNEL)
            .setSmallIcon(getApplicationInfo().icon)
            .setContentTitle(title)
            .setContentText(body)
            .setStyle(new Notification.BigTextStyle().bigText(body))
            .setAutoCancel(true)
            .setContentIntent(tap)
            .build();
        NotificationManager nm = (NotificationManager) getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm != null) nm.notify((int) (System.currentTimeMillis() & 0x7fffffff), note);
    }

    @Override
    public void onNewToken(String token) {
        // Topics follow the install, so nothing to store yet (per-player messages come later).
    }
}
