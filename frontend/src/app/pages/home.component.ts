import { Component } from '@angular/core';

@Component({
  selector: 'app-home',
  template: '<section class="cd-reading"><p>Operations workspace</p><h1>Plan events with confidence.</h1><p>Authorized administrators can create and manage Draft Events.</p></section>',
  styles: ['h1 { font-family: var(--cd-font-display); font-size: clamp(2rem, 8vw, 4rem); line-height: var(--cd-line-tight); margin-block: var(--cd-space-3); } p { color: var(--cd-text-secondary); }'],
})
export class HomeComponent {}
