package com.smartcity.air;

import android.app.Activity;
import android.media.*;
import android.media.audiofx.AcousticEchoCanceler;
import android.util.Base64;
import java.util.concurrent.ArrayBlockingQueue;

/** No recording files. Bounded queues, PCM16 mono 16 kHz in / 24 kHz out. */
public final class AirVoiceAudio {
    private volatile boolean running, muted;
    private volatile String error = "";
    private final ArrayBlockingQueue<String> input = new ArrayBlockingQueue<>(25);
    private static final class Frame { final byte[] pcm; final int generation;
        Frame(byte[] pcm, int generation) { this.pcm = pcm; this.generation = generation; }
    }
    private final ArrayBlockingQueue<Frame> output = new ArrayBlockingQueue<>(128);
    private AudioRecord recorder;
    private AudioTrack player;
    private AcousticEchoCanceler aec;
    private AudioManager manager;
    private int previousMode;
    private Thread captureThread, playThread;
    private final Object playbackLock = new Object();
    private volatile int generation;
    public String start(Activity activity, boolean capture) {
        try {
            manager = (AudioManager)activity.getSystemService(Activity.AUDIO_SERVICE);
            previousMode = manager.getMode(); manager.setMode(AudioManager.MODE_IN_COMMUNICATION);
            int size = Math.max(9600, AudioTrack.getMinBufferSize(24000, AudioFormat.CHANNEL_OUT_MONO, AudioFormat.ENCODING_PCM_16BIT));
            player = new AudioTrack.Builder().setAudioAttributes(new AudioAttributes.Builder()
                .setUsage(AudioAttributes.USAGE_VOICE_COMMUNICATION).setContentType(AudioAttributes.CONTENT_TYPE_SPEECH).build())
                .setAudioFormat(new AudioFormat.Builder().setSampleRate(24000).setChannelMask(AudioFormat.CHANNEL_OUT_MONO)
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT).build()).setBufferSizeInBytes(size).setTransferMode(AudioTrack.MODE_STREAM).build();
            if (player.getState() != AudioTrack.STATE_INITIALIZED) throw new IllegalStateException();
            player.play(); running = true;
            playThread = new Thread(() -> {
                try { while (running) {
                    Frame frame = output.take(); int current = frame.generation; byte[] pcm = frame.pcm;
                    for (int at = 0; running && current == generation && at < pcm.length;) {
                        int count;
                        synchronized (playbackLock) {
                            if (current != generation || !running) break;
                            count = player.write(pcm, at, Math.min(1920, pcm.length - at), AudioTrack.WRITE_NON_BLOCKING);
                        }
                        if (count < 0) { error = "playback_failed"; break; }
                        at += count; if (count == 0) Thread.sleep(5);
                    }
                }} catch (InterruptedException ignored) {} catch (Exception e) { error = "playback_failed"; }
            }, "AirVoicePlayback"); playThread.start();
            if (capture) {
                size = Math.max(6400, AudioRecord.getMinBufferSize(16000, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT));
                recorder = new AudioRecord(MediaRecorder.AudioSource.VOICE_COMMUNICATION, 16000,
                    AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_16BIT, size);
                if (recorder.getState() != AudioRecord.STATE_INITIALIZED) throw new IllegalStateException();
                if (AcousticEchoCanceler.isAvailable()) {
                    aec = AcousticEchoCanceler.create(recorder.getAudioSessionId()); if (aec != null) aec.setEnabled(true);
                }
                recorder.startRecording();
                captureThread = new Thread(() -> {
                    byte[] pcm = new byte[1280];
                    try { while (running) {
                        int n = recorder.read(pcm, 0, pcm.length);
                        if (n < 0) { if (running) error = "capture_failed"; break; }
                        if (n > 0 && !muted && !input.offer(Base64.encodeToString(pcm, 0, n, Base64.NO_WRAP))) {
                            error = "capture_overflow"; break;
                        }
                    }} catch (Exception e) { if (running) error = "capture_failed"; }
                }, "AirVoiceCapture"); captureThread.start();
            }
            return aec != null && aec.getEnabled() ? "aec_enabled" : "aec_unavailable";
        } catch (Exception e) { stop(); return "audio_failed"; }
    }
    public String poll() { String item = input.poll(); return item == null ? "" : item; }
    public String error() { return error; }
    public void play(String base64) {
        if (!running) return;
        if (!output.offer(new Frame(Base64.decode(base64, Base64.DEFAULT), generation))) error = "playback_overflow";
    }
    public void interrupt() {
        synchronized (playbackLock) {
            generation++; output.clear();
            if (player != null) { player.pause(); player.flush(); if (running) player.play(); }
        }
    }
    public void mute(boolean value) { muted = value; input.clear(); }
    public void stop() {
        running = false; input.clear();
        if (recorder != null) { try { recorder.stop(); } catch (Exception ignored) {} }
        if (captureThread != null) { try { captureThread.join(500); } catch (InterruptedException ignored) {} captureThread = null; }
        if (playThread != null) { playThread.interrupt(); try { playThread.join(500); } catch (InterruptedException ignored) {} playThread = null; }
        synchronized (playbackLock) {
            output.clear();
            if (player != null) { try { player.pause(); player.flush(); player.release(); } catch (Exception ignored) {} player = null; }
        }
        if (aec != null) { aec.release(); aec = null; }
        if (recorder != null) { recorder.release(); recorder = null; }
        if (manager != null) { manager.setMode(previousMode); manager = null; }
    }
}
