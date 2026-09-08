import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
describe('App', () => {
  it('shows navigation to portfolio and all six registrations', async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const links = fixture.nativeElement.querySelectorAll('nav a');
    expect(links.length).toBe(7);
    expect(fixture.nativeElement.textContent).toContain('Investment');
    expect(fixture.nativeElement.querySelector('a[href="/currencies"]')).toBeTruthy();
  });
});
