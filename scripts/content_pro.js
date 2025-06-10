// [MERGED-PRO 2025-05-20] Improved region-capture with devicePixelRatio fix
(() => {
  if (window.__sarProInjected) return;
  window.__sarProInjected = true;

  let stream, recorder, chunks = [], overlay, selection;
  const DPR = window.devicePixelRatio || 1;

  async function selectArea() {
    return new Promise(resolve => {
      overlay = document.createElement('div');
      overlay.style.cssText = `
        position:fixed;inset:0;cursor:crosshair;z-index:2147483647;
        background:rgba(0,0,0,.15);
      `;
      document.body.appendChild(overlay);
      let start = null, rectEl = document.createElement('div');
      rectEl.style.cssText = 'position:absolute;border:2px dashed #0d8fff;background:rgba(13,143,255,.15)';
      overlay.appendChild(rectEl);

      overlay.addEventListener('mousedown', e => {
        start = {x:e.clientX, y:e.clientY};
      }, {once:true});

      overlay.addEventListener('mousemove', e => {
        if (!start) return;
        const x = Math.min(start.x, e.clientX);
        const y = Math.min(start.y, e.clientY);
        const w = Math.abs(start.x - e.clientX);
        const h = Math.abs(start.y - e.clientY);
        Object.assign(rectEl.style, {left:x+'px',top:y+'px',width:w+'px',height:h+'px'});
      });

      overlay.addEventListener('mouseup', e => {
        const x = Math.min(start.x, e.clientX);
        const y = Math.min(start.y, e.clientY);
        const w = Math.abs(start.x - e.clientX);
        const h = Math.abs(start.y - e.clientY);
        selection = {x, y, width: w, height: h}; // Use CSS pixels as spec
        overlay.remove();
        resolve(selection);
      }, {once:true});
    });
  }

  async function startRecord() {
    const area = await selectArea();
    stream = await navigator.mediaDevices.getDisplayMedia({
                video: {
                  displaySurface: 'browser',
                  selfBrowserSurface: 'exclude',
                  surfaceSwitching: 'exclude',
                  preferCurrentTab: true
                },
                audio: true
            });
    const [track] = stream.getVideoTracks();
    if (track.cropTo) {
      const cropTarget = await CropTarget.fromRect(area);
      await track.cropTo(cropTarget);
    } else if (track.applyConstraints) {
      await track.applyConstraints({advanced:[{width:area.width,height:area.height,pan:area.x,tilt:area.y}]});
    }
    recorder = new MediaRecorder(stream, {mimeType:'video/webm;codecs=vp9'});
    recorder.ondataavailable = e=>chunks.push(e.data);
    recorder.onstop = ()=>{
      const blob = new Blob(chunks,{type:'video/webm'});
      const url = URL.createObjectURL(blob);
      chrome.runtime.sendMessage({ type: 'download', url, filename:'recording-'+Date.now()+'.webm' });
      chunks=[];stream.getTracks().forEach(t=>t.stop());
    };
    recorder.start();
  }

  // Enhance frame recording capabilities
  function enhanceFrameRecording(stream) {
    if (window.__sarAutoscrollTarget && window.__sarAutoscrollTarget.contentWindow) {
      const frameDoc = window.__sarAutoscrollTarget.contentDocument || window.__sarAutoscrollTarget.contentWindow.document;
      
      // Set up frame-specific scroll listener
      frameDoc.addEventListener('scroll', () => {
        if (autoscrollSettings.enabled) {
          updateRecordingPosition();
        }
      });
    }
  }

  // Update the recording position based on frame scroll
  function updateRecordingPosition() {
    if (window.__sarAutoscrollTarget) {
      const frameRect = window.__sarAutoscrollTarget.getBoundingClientRect();
      const frameScroll = {
        x: window.__sarAutoscrollTarget.contentWindow.scrollX,
        y: window.__sarAutoscrollTarget.contentWindow.scrollY
      };
      
      area = {
        x: frameRect.left + window.scrollX,
        y: frameRect.top + window.scrollY,
        width: frameRect.width,
        height: frameRect.height,
        frameScroll
      };
      
      send({type: 'sar-update-area', area});
    }
  }

  // Enhance existing startRecording function
  const originalStartRecording = startRecording;
  startRecording = async function(...args) {
    const stream = await originalStartRecording.apply(this, args);
    enhanceFrameRecording(stream);
    return stream;
  };

  function enhanceFrameDetection(e) {
    const frameElement = findFrameUnderCursor(e);
    if (!frameElement) {
      console.log('[SAR PRO] No scrollable frame/element found');
      return false;
    }
    
    // Enhanced tracking for pro features
    if (frameElement.tagName === 'IFRAME' || frameElement.tagName === 'FRAME') {
      try {
        // Store frame metadata for better tracking
        window.__sarFrameData = {
          type: frameElement.tagName.toLowerCase(),
          src: frameElement.src,
          dimensions: {
            width: frameElement.clientWidth,
            height: frameElement.clientHeight
          }
        };
      } catch (e) {
        console.log('[SAR PRO] CORS restriction on frame access');
      }
    }
    
    return true;
  }

  // Enhance existing selection handlers
  const originalSelectFrame = selectFrame;
  selectFrame = function(frameElement) {
    if (enhanceFrameDetection({ target: frameElement })) {
      originalSelectFrame(frameElement);
    }
  };

  chrome.runtime.onMessage.addListener((msg)=> {
    if (msg && msg.sarProStart) startRecord();
  });
})();
