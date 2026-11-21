const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const page = fs.readFileSync(path.join(__dirname,
    '..', 'src', 'CasCap.App.Maui', 'Resources', 'Raw', 'wwwroot', 'index.html'), 'utf8');
const script = page.match(/<script>([\s\S]*?)<\/script>/)[1];

function harness() {
    let now = 0;
    let nextTimer = 0;
    const timers = new Map();
    const messages = [];
    const connections = [];
    const tracks = [];
    const remote = {};
    const context = vm.createContext({
        console,
        document: { getElementById: () => remote },
        window: { HybridWebView: { SendRawMessage: json => messages.push(JSON.parse(json)) } },
        navigator: { mediaDevices: { getUserMedia: async () => {
            const track = { enabled: true, stopped: false, stop() { this.stopped = true; } };
            tracks.push(track);
            return { getTracks: () => [track], getAudioTracks: () => [track] };
        } } },
        RTCPeerConnection: class {
            constructor() { connections.push(this); this.connectionState = 'new'; }
            addTrack() {}
            createDataChannel() {
                const sent = [];
                this.channel = {
                    readyState: 'open',
                    sent,
                    send: json => sent.push(JSON.parse(json)),
                };
                return this.channel;
            }
            async createOffer() { return { sdp: 'synthetic-offer' }; }
            async setLocalDescription(offer) { this.localDescription = offer; }
            async setRemoteDescription() {}
            close() { this.connectionState = 'closed'; this.onconnectionstatechange?.(); }
        },
        AudioContext: class {
            createAnalyser() { return { fftSize: 512 }; }
            createMediaStreamSource() { return { connect() {} }; }
            close() {}
        },
        setInterval: () => 1,
        clearInterval() {},
        setTimeout: (callback, delay) => {
            const id = ++nextTimer;
            timers.set(id, { callback, at: now + delay });
            return id;
        },
        clearTimeout: id => timers.delete(id),
    });
    vm.runInContext(script, context);
    return {
        context, messages, connections, tracks, remote,
        providerEvent(event) {
            connections.at(-1).channel.onmessage({ data: JSON.stringify(event) });
        },
        state(value, connection = connections.at(-1)) {
            connection.connectionState = value;
            connection.onconnectionstatechange();
        },
        advance(milliseconds) {
            now += milliseconds;
            for (const [id, timer] of timers)
                if (timer.at <= now) { timers.delete(id); timer.callback(); }
        },
    };
}

test('speech pace waits for the active reply and only updates provider audio speed', async () => {
    const h = harness();
    await h.context.createOffer();
    const { sent } = h.connections[0].channel;
    h.providerEvent({ type: 'response.created', response: { id: 'reply' } });
    const applied = h.context.setSpeechSpeed(0.8);
    assert.equal(sent.length, 0);
    h.providerEvent({ type: 'response.done', response: { id: 'reply' } });
    assert.deepEqual(sent, [{
        type: 'session.update',
        event_id: 'speech-pace-1',
        session: { type: 'realtime', audio: { output: { speed: 0.8 } } },
    }]);
    h.providerEvent({ type: 'session.updated', session: { audio: { output: { speed: 0.8 } } } });
    assert.equal(await applied, true);
});

test('speech pace rejects provider errors and times out without acknowledgement', async () => {
    const h = harness();
    await h.context.createOffer();
    const fast = h.context.setSpeechSpeed(1.2);
    h.providerEvent({ type: 'error', error: { event_id: 'speech-pace-1' } });
    assert.equal(await fast, false);

    const normal = h.context.setSpeechSpeed(1);
    h.providerEvent({ type: 'session.updated', session: { audio: { output: { speed: 1.2 } } } });
    h.advance(15_000);
    assert.equal(await normal, false);
});

test('hang-up discards a queued pace update rather than sending it on the next call', async () => {
    const h = harness();
    await h.context.createOffer();
    h.providerEvent({ type: 'response.created', response: { id: 'old-reply' } });
    const queued = h.context.setSpeechSpeed(0.8);
    h.context.hangUp();
    assert.equal(await queued, false);
    await h.context.createOffer();
    assert.deepEqual(h.connections.at(-1).channel.sent, []);
});

test('disconnect gets exactly twenty seconds, including duplicate notifications', async () => {
    const h = harness();
    await h.context.createOffer();
    h.state('disconnected');
    h.advance(10_000);
    h.state('disconnected');
    h.advance(9_999);
    assert.deepEqual(h.messages, [{ type: 'state', value: 'reconnecting' }]);
    h.advance(1);
    assert.deepEqual(h.messages.at(-1), { type: 'state', value: 'failed' });
});

test('connected cancels recovery timeout, but terminal failure is immediate', async () => {
    const h = harness();
    await h.context.createOffer();
    h.state('disconnected');
    h.advance(19_999);
    h.state('connected');
    h.advance(20_000);
    assert.equal(h.messages.filter(message => message.value === 'failed').length, 0);
    assert.equal(h.messages.at(-1).value, 'connected');
    h.state('failed');
    assert.equal(h.messages.at(-1).value, 'failed');
});

test('hang-up releases audio and ignores stale callbacks from a previous call', async () => {
    const h = harness();
    await h.context.createOffer();
    const old = h.connections[0];
    h.state('disconnected');
    h.context.hangUp();
    assert.equal(h.tracks[0].stopped, true);
    assert.equal(h.remote.srcObject, null);
    await h.context.createOffer();
    h.state('connected');
    h.state('failed', old);
    h.advance(20_000);
    assert.equal(h.messages.at(-1).value, 'connected');
});

test('permission completing after cancellation cannot leave the microphone running', async () => {
    const h = harness();
    let release;
    let stopped = false;
    h.context.navigator.mediaDevices.getUserMedia = () => new Promise(resolve => { release = resolve; });
    const offer = h.context.createOffer();
    h.context.hangUp();
    release({ getTracks: () => [{ stop() { stopped = true; } }] });
    assert.equal(await offer, null);
    assert.equal(stopped, true);
    assert.equal(h.connections.length, 0);
});

test('late SDP completion from an ended call cannot replace or close a newer call', { timeout: 2_000 }, async () => {
    const h = harness();
    const original = h.context.RTCPeerConnection.prototype.createOffer;
    let release;
    let markStarted;
    const started = new Promise(resolve => { markStarted = resolve; });
    let first = true;
    h.context.RTCPeerConnection.prototype.createOffer = function () {
        if (!first)
            return original.call(this);
        first = false;
        markStarted();
        return new Promise(resolve => { release = resolve; });
    };
    const oldOffer = h.context.createOffer();
    await started;
    h.context.hangUp();
    assert.equal(await h.context.createOffer(), 'synthetic-offer');
    release({ sdp: 'stale-offer' });
    assert.equal(await oldOffer, null);
    assert.equal(h.tracks[0].stopped, true);
    assert.equal(h.tracks[1].stopped, false);
    assert.notEqual(h.connections[1].connectionState, 'closed');
});
