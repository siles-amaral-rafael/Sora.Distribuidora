window.soraSignature = {
    init(canvas) {
        const ctx = canvas.getContext('2d');
        ctx.lineWidth = 2.5; ctx.lineCap = 'round'; ctx.strokeStyle = '#172554';
        let drawing = false;
        const point = e => { const r = canvas.getBoundingClientRect(); return { x: (e.clientX-r.left)*canvas.width/r.width, y: (e.clientY-r.top)*canvas.height/r.height }; };
        canvas.onpointerdown = e => { drawing = true; canvas.setPointerCapture(e.pointerId); const p=point(e); ctx.beginPath(); ctx.moveTo(p.x,p.y); };
        canvas.onpointermove = e => { if(!drawing)return; const p=point(e); ctx.lineTo(p.x,p.y); ctx.stroke(); };
        canvas.onpointerup = canvas.onpointercancel = () => { drawing=false; };
    },
    data(canvas) { const blank=document.createElement('canvas'); blank.width=canvas.width; blank.height=canvas.height; return canvas.toDataURL('image/png')===blank.toDataURL('image/png')?null:canvas.toDataURL('image/png').split(',')[1]; },
    clear(canvas) { canvas.getContext('2d').clearRect(0,0,canvas.width,canvas.height); }
};
