import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { DraftEventsService } from '../../data/draft-events.service';
import { DraftListComponent } from './draft-list.component';

describe('DraftListComponent', () => {
  it('renders Drafts as links that can be reopened', async () => {
    await TestBed.configureTestingModule({
      imports: [DraftListComponent],
      providers: [
        provideRouter([]),
        { provide: DraftEventsService, useValue: { listDrafts: () => of({ items: [{
          id: '10000000-0000-0000-0000-000000000001', title: 'Beach cleanup',
          start: { localDateTime: '2026-10-10T14:30:00', offset: '+03:00' },
          location: 'North shore', status: 'Draft',
        }], page: 1, pageSize: 20 }) } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(DraftListComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('.draft-card');
    expect(link?.textContent).toContain('Beach cleanup');
    expect(link?.getAttribute('href')).toBe('/draft-events/10000000-0000-0000-0000-000000000001');
  });
});
