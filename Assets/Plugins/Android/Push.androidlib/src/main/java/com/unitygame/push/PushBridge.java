package com.unitygame.push;

import android.app.Activity;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

import com.google.firebase.messaging.FirebaseMessaging;

/** Called from Push.cs: notification channel, "all" topic, and links from tapped notifications. */
public final class PushBridge {
    static final String TAG = "TTPush";
    public static final String CHANNEL = "news";
    public static final String TOPIC_ALL = "all";

    private PushBridge() {}

    public static void init(Activity activity) {
        ensureChannel(activity);
        try {
            FirebaseMessaging fm = FirebaseMessaging.getInstance();
            fm.setAutoInitEnabled(true);
            fm.subscribeToTopic(TOPIC_ALL).addOnCompleteListener(t ->
                Log.i(TAG, "topic '" + TOPIC_ALL + "' " + (t.isSuccessful() ? "subscribed" : "failed: " + t.getException())));
            fm.getToken().addOnCompleteListener(t -> {
                if (t.isSuccessful()) Log.i(TAG, "token " + t.getResult());
            });
        } catch (Exception e) {
            Log.w(TAG, "init failed", e);
        }
    }

    static void ensureChannel(Context ctx) {
        NotificationManager nm = (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null || nm.getNotificationChannel(CHANNEL) != null) return;
        NotificationChannel ch = new NotificationChannel(CHANNEL, "News & events", NotificationManager.IMPORTANCE_DEFAULT);
        ch.setDescription("New pens, events and announcements");
        nm.createNotificationChannel(ch);
    }

    /**
     * A link carried by a tapped notification (custom data key "link"), or "" if none.
     * Read once: the extra is removed so it doesn't fire again.
     */
    public static String takeLink(Activity activity) {
        Intent i = activity.getIntent();
        if (i == null || i.getExtras() == null) return "";
        String link = i.getStringExtra("link");
        if (link == null) return "";
        i.removeExtra("link");
        return link;
    }
}
