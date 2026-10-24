package com.unitygame.googlesignin;

import android.app.Activity;
import android.os.CancellationSignal;

import androidx.credentials.ClearCredentialStateRequest;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.ClearCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;

import java.util.concurrent.Executor;
import java.util.concurrent.Executors;

/**
 * Google sign-in for Tuition Takkar through Android's Credential Manager.
 * Unity calls signIn(...) and gets the Google ID token back through Listener,
 * then hands that token to Unity Authentication (SignInWithGoogleAsync / LinkWithGoogleAsync).
 */
public final class GoogleSignInBridge {

    /** Implemented in C# with AndroidJavaProxy. Called on a background thread. */
    public interface Listener {
        void onSuccess(String idToken, String displayName);
        void onError(String code, String message);
    }

    private static final Executor EXECUTOR = Executors.newSingleThreadExecutor();

    private GoogleSignInBridge() {}

    /** Shows the "Sign in with Google" sheet. serverClientId is the Web client ID. */
    public static void signIn(final Activity activity, final String serverClientId, final Listener listener) {
        try {
            GetSignInWithGoogleOption option = new GetSignInWithGoogleOption.Builder(serverClientId).build();
            GetCredentialRequest request = new GetCredentialRequest.Builder()
                    .addCredentialOption(option)
                    .build();

            CredentialManager manager = CredentialManager.create(activity);
            manager.getCredentialAsync(activity, request, new CancellationSignal(), EXECUTOR,
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override
                        public void onResult(GetCredentialResponse response) {
                            Credential credential = response.getCredential();
                            if (credential instanceof CustomCredential
                                    && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                try {
                                    GoogleIdTokenCredential google = GoogleIdTokenCredential.createFrom(credential.getData());
                                    String name = google.getDisplayName();
                                    listener.onSuccess(google.getIdToken(), name == null ? "" : name);
                                } catch (Exception e) {
                                    listener.onError("parse", String.valueOf(e.getMessage()));
                                }
                            } else {
                                listener.onError("type", "Unexpected credential type");
                            }
                        }

                        @Override
                        public void onError(GetCredentialException e) {
                            String code = "failed";
                            if (e instanceof GetCredentialCancellationException) code = "cancelled";
                            else if (e instanceof NoCredentialException) code = "no_account";
                            listener.onError(code, String.valueOf(e.getMessage()));
                        }
                    });
        } catch (Throwable t) {
            listener.onError("failed", String.valueOf(t.getMessage()));
        }
    }

    /** Forget the chosen Google account so the next sign-in shows the account picker again. */
    public static void signOut(final Activity activity) {
        try {
            CredentialManager.create(activity).clearCredentialStateAsync(new ClearCredentialStateRequest(),
                    new CancellationSignal(), EXECUTOR,
                    new CredentialManagerCallback<Void, ClearCredentialException>() {
                        @Override public void onResult(Void v) {}
                        @Override public void onError(ClearCredentialException e) {}
                    });
        } catch (Throwable ignored) {}
    }
}
