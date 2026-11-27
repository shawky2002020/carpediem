import { Component, input } from '@angular/core';

const paths = {
  home: 'm3 11 9-7 9 7 M6 10v10h12V10 M10 20v-6h4v6',
  dashboard: 'M4 5h16v15H4z M4 9h16 M8 3v4 M16 3v4 M8 13h3 M14 13h2 M8 16h3',
  drafts: 'M6 3h9l4 4v14H6z M14 3v5h5 M9 12h7 M9 16h5',
  accounts: 'M14 19H3v-2a5.5 5.5 0 0 1 11 0z M8.5 10a3 3 0 1 0 0-6 3 3 0 0 0 0 6 M16 5a3 3 0 0 1 0 6 M17 13a5 5 0 0 1 4 5v1h-4',
  search: 'M10.5 17a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13z M15.2 15.2 20 20',
  filters: 'M4 7h5 M13 7h7 M9 5v4 M4 12h10 M18 12h2 M14 10v4 M4 17h3 M11 17h9 M7 15v4',
  close: 'm6 6 12 12 M18 6 6 18',
  chevron: 'm9 5 7 7-7 7',
  back: 'M20 12H4m7-7-7 7 7 7',
  feedback: 'M4 4h16v12h-8l-6 4v-4H4z M8 8h8 M8 12h5',
  incidents: 'm12 3 9 16H3z M12 9v4 M12 15v1',
  appointments: 'M4 5h16v15H4z M4 9h16 M8 3v4 M16 3v4 m-7 7 2 2 4-4',
  profile: 'M12 11a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7 M5 21v-2a7 6 0 0 1 14 0v2',
  inbox: 'M5 18h14l-2-3V9a5 5 0 0 0-10 0v6z M10 20a2.3 2.3 0 0 0 4 0',
  exit: 'M10 4H4v16h6 M9 12h12 M17 8l4 4-4 4',
};

@Component({
  selector: 'app-navigation-icon',
  host: { 'aria-hidden': 'true' },
  template: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.65" stroke-linejoin="round" stroke-linecap="round"><path [attr.d]="paths[name()]" /></svg>',
  styles: ':host { display:inline-flex; inline-size:1.25rem; block-size:1.25rem; flex:none; } svg {inline-size:100%;block-size:100%;}',
})
export class NavigationIconComponent {
  readonly name = input.required<keyof typeof paths>();
  protected readonly paths = paths;
}
