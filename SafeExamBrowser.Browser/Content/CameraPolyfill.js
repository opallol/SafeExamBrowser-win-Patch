(function() {
    if (navigator.mediaDevices) {
        var originalGetUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
        var originalEnumerateDevices = navigator.mediaDevices.enumerateDevices.bind(navigator.mediaDevices);
        
        navigator.mediaDevices.enumerateDevices = function() {
            return originalEnumerateDevices().then(function(devices) {
                var fakeVideoDevice = {
                    deviceId: 'seb-virtual-camera',
                    kind: 'videoinput',
                    label: 'SEB Virtual Camera',
                    groupId: 'seb-group'
                };
                // Remove real video inputs and add the fake one
                var filtered = devices.filter(function(d) { return d.kind !== 'videoinput'; });
                filtered.push(fakeVideoDevice);
                return filtered;
            });
        };

        navigator.mediaDevices.getUserMedia = function(constraints) {
            return new Promise(function(resolve, reject) {
                if (constraints && constraints.video) {
                    var canvas = document.createElement('canvas');
                    canvas.width = 640;
                    canvas.height = 480;
                    // Keep canvas hidden but in DOM so captureStream works
                    canvas.style.position = 'fixed';
                    canvas.style.left = '-9999px';
                    canvas.style.top = '-9999px';
                    canvas.style.width = '1px';
                    canvas.style.height = '1px';
                    canvas.style.opacity = '0.01';
                    canvas.style.pointerEvents = 'none';

                    var targetContainer = document.body || document.documentElement;
                    if (targetContainer) {
                        targetContainer.appendChild(canvas);
                    }

                    var ctx = canvas.getContext('2d');
                    // Fill black initially so the track is never empty
                    ctx.fillStyle = '#000000';
                    ctx.fillRect(0, 0, canvas.width, canvas.height);

                    var isStopped = false;
                    var isPending = false;
                    var firstFrameDrawn = false;
                    var resolveStream = null;
                    var streamReadyPromise = new Promise(function(r) { resolveStream = r; });

                    // Poll and draw directly in onload - no requestAnimationFrame
                    // This keeps working even when the tab is backgrounded
                    var pollSnapshot = function() {
                        if (isStopped) return;
                        if (isPending) {
                            // Previous request still in flight, wait a bit
                            setTimeout(pollSnapshot, 50);
                            return;
                        }
                        isPending = true;
                        var img = new Image();
                        img.crossOrigin = 'anonymous';
                        img.onload = function() {
                            isPending = false;
                            if (!isStopped) {
                                try {
                                    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
                                    if (!firstFrameDrawn) {
                                        firstFrameDrawn = true;
                                        resolveStream();
                                    }
                                } catch(e) {}
                                // Schedule next poll at ~10 FPS
                                setTimeout(pollSnapshot, 100);
                            }
                        };
                        img.onerror = function() {
                            isPending = false;
                            if (!isStopped) {
                                setTimeout(pollSnapshot, 300);
                            }
                        };
                        img.src = 'http://localhost:18923/snapshot/?t=' + Date.now();
                    };

                    pollSnapshot();

                    // Timeout fallback in case camera is completely dead
                    setTimeout(function() {
                        if (!firstFrameDrawn) {
                            firstFrameDrawn = true;
                            resolveStream();
                        }
                    }, 8000);

                    // Use captureStream with 10 so browser automatically captures frames
                    var stream = canvas.captureStream(10);
                    var videoTrack = stream.getVideoTracks()[0];

                    if (videoTrack) {
                        var originalStop = videoTrack.stop.bind(videoTrack);
                        videoTrack.stop = function() {
                            isStopped = true;
                            if (canvas.parentNode) {
                                canvas.parentNode.removeChild(canvas);
                            }
                            originalStop();
                        };

                        Object.defineProperty(videoTrack, 'label', {
                            get: function() { return 'SEB Virtual Camera'; }
                        });
                        Object.defineProperty(videoTrack, 'muted', {
                            get: function() { return false; }
                        });
                        Object.defineProperty(videoTrack, 'readyState', {
                            get: function() { return 'live'; }
                        });
                        Object.defineProperty(videoTrack, 'enabled', {
                            get: function() { return true; },
                            set: function(v) {}
                        });
                        videoTrack.getSettings = function() {
                            return {
                                deviceId: 'seb-virtual-camera',
                                width: 640,
                                height: 480,
                                frameRate: 10
                            };
                        };
                        videoTrack.getCapabilities = function() {
                            return {
                                deviceId: 'seb-virtual-camera',
                                width: { max: 640, min: 640 },
                                height: { max: 480, min: 480 },
                                frameRate: { max: 10, min: 10 }
                            };
                        };
                    }
                    
                    streamReadyPromise.then(function() {
                        if (constraints.audio) {
                            originalGetUserMedia({ audio: true }).then(function(audioStream) {
                                audioStream.getAudioTracks().forEach(function(track) {
                                    stream.addTrack(track);
                                });
                                resolve(stream);
                            }).catch(function(err) {
                                console.warn("Failed to get audio track", err);
                                resolve(stream);
                            });
                        } else {
                            resolve(stream);
                        }
                    });
                } else {
                    originalGetUserMedia(constraints).then(resolve).catch(reject);
                }
            });
        };
    }
})();
