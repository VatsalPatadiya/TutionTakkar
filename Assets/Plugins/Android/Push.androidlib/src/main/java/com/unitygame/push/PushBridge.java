package com.unitygame.push;

import android.app.Activity;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

import com.google.firebase.messaging.FirebaseMessaging;

import java.util.Map;
import java.util.concurrent.ConcurrentLinkedQueue;

/** Called from Push.cs: notification channel, "all" topic, and links from tapped notifications. */
public final class PushBridge {
    static final String TAG = "TTPush";
    public static final String CHANNEL = "news";
    public static final String TOPIC_ALL = "all";
    public static final String CHANNEL_INVITES = "invites";
    static volatile String lastToken = "";

    /** True while the game is on screen: invites and friend messages go to the game instead of the tray. */
    static volatile boolean foreground = false;
    /** Messages for the game, one per entry: key \u001f value \u001e key \u001f value ... */
    static final ConcurrentLinkedQueue<String> inbox = new ConcurrentLinkedQueue<>();

    /** This install's FCM token ("" until Firebase hands it over). */
    public static String token() { return lastToken == null ? "" : lastToken; }

    public static void setForeground(boolean on) { foreground = on; }

    /** Everything waiting for the game, messages separated by \u001d ("" when nothing). */
    public static String takeInbox() {
        StringBuilder sb = new StringBuilder();
        String m;
        while ((m = inbox.poll()) != null) {
            if (sb.length() > 0) sb.append('\u001d');
            sb.append(m);
        }
        return sb.toString();
    }

    /** Hands a message to the open game. False when the game isn't open or the message isn't for it. */
    static boolean deliverToGame(Map<String, String> data) {
        if (!foreground || data == null || data.get("kind") == null) return false;
        StringBuilder sb = new StringBuilder();
        for (Map.Entry<String, String> e : data.entrySet()) {
            if (sb.length() > 0) sb.append('\u001e');
            sb.append(clean(e.getKey())).append('\u001f').append(clean(e.getValue()));
        }
        inbox.add(sb.toString());
        Log.i(TAG, "message for the open game: " + data.get("kind"));
        return true;
    }

    static String clean(String s) {
        if (s == null) return "";
        return s.replace('\u001d', ' ').replace('\u001e', ' ').replace('\u001f', ' ');
    }

    private PushBridge() {}

    public static void init(Activity activity) {
        ensureChannel(activity);
        foreground = true;
        try {
            FirebaseMessaging fm = FirebaseMessaging.getInstance();
            fm.setAutoInitEnabled(true);
            fm.subscribeToTopic(TOPIC_ALL).addOnCompleteListener(t ->
                Log.i(TAG, "topic '" + TOPIC_ALL + "' " + (t.isSuccessful() ? "subscribed" : "failed: " + t.getException())));
            fm.getToken().addOnCompleteListener(t -> {
                if (t.isSuccessful()) { lastToken = t.getResult(); Log.i(TAG, "token ready"); }
                else Log.w(TAG, "token failed: " + t.getException());
            });
        } catch (Exception e) {
            Log.w(TAG, "init failed", e);
        }
    }

    static void ensureChannel(Context ctx) {
        NotificationManager nm = (NotificationManager) ctx.getSystemService(Context.NOTIFICATION_SERVICE);
        if (nm == null) return;
        if (nm.getNotificationChannel(CHANNEL) == null) {
            NotificationChannel ch = new NotificationChannel(CHANNEL, "News & events", NotificationManager.IMPORTANCE_DEFAULT);
            ch.setDescription("New pens, events and announcements");
            nm.createNotificationChannel(ch);
        }
        if (nm.getNotificationChannel(CHANNEL_INVITES) == null) {
            NotificationChannel ch = new NotificationChannel(CHANNEL_INVITES, "Friends & room invites", NotificationManager.IMPORTANCE_HIGH);
            ch.setDescription("Friend requests and seats saved for you in a friend's room");
            nm.createNotificationChannel(ch);
        }
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
