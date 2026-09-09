import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TargetsPage } from './targets-page';
import { testPortfolio } from './allocation.test-data';

describe('TargetsPage', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TargetsPage],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());
  function initialize() {
    const fixture = TestBed.createComponent(TargetsPage);
    fixture.detectChanges();
    http.expectOne('/api/portfolios').flush([testPortfolio]);
    http.expectOne('/api/asset-categories').flush([
      { id: 1, name: 'Ações' },
      { id: 2, name: 'FIIs' },
    ]);
    http
      .expectOne('/api/portfolios/1/category-targets')
      .flush([{ groupId: 1, name: 'Ações', targetPercentage: '1' }]);
    fixture.detectChanges();
    return fixture;
  }
  it('edits Brazilian percentages and sends one exact complete set', () => {
    const fixture = initialize();
    expect(fixture.nativeElement.querySelector('#target-0').value).toBe('100,0000');
    for (const [index, value] of ['33,3333', '66,6667'].entries()) {
      const input: HTMLInputElement = fixture.nativeElement.querySelector('#target-' + index);
      input.value = value;
      input.dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
    expect(fixture.componentInstance.total()).toBe('100,0000');
    fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
    const request = http.expectOne('/api/portfolios/1/category-targets');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({
      targets: [
        { groupId: 1, targetPercentage: '0.333333' },
        { groupId: 2, targetPercentage: '0.666667' },
      ],
    });
    request.flush(null);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Metas salvas');
  });
  it('preserves the edited set when the server rejects the total', () => {
    const fixture = initialize();
    fixture.componentInstance.rows.at(0).controls.percentage.setValue('40');
    fixture.componentInstance.save();
    http
      .expectOne('/api/portfolios/1/category-targets')
      .flush(
        { detail: 'A soma das metas deve ser exatamente 100%.' },
        { status: 400, statusText: 'Bad Request' },
      );
    fixture.detectChanges();
    expect(fixture.componentInstance.rows.at(0).controls.percentage.value).toBe('40');
    expect(fixture.nativeElement.textContent).toContain('exatamente 100%');
  });
  it('loads sector targets independently and cancels an outdated request', () => {
    const fixture = initialize();
    fixture.componentInstance.changeDimension('sector');
    const options = http.expectOne('/api/sectors');
    const oldRequest = http.expectOne('/api/portfolios/1/sector-targets');
    fixture.componentInstance.select(2);
    expect(options.cancelled).toBe(true);
    expect(oldRequest.cancelled).toBe(true);
    http.expectOne('/api/sectors').flush([{ id: 5, name: 'Tecnologia' }]);
    http.expectOne('/api/portfolios/2/sector-targets').flush([]);
    fixture.componentInstance.rows.at(0).controls.percentage.setValue('100');
    fixture.componentInstance.save();
    http.expectOne('/api/portfolios/2/sector-targets').flush(null);
  });
  it('rejects excess precision and exposes a retry after loading fails', () => {
    const fixture = initialize();
    fixture.componentInstance.rows.at(0).controls.percentage.setValue('33,33333');
    fixture.componentInstance.save();
    http.expectNone('/api/portfolios/1/category-targets');
    fixture.componentInstance.load();
    http.expectOne('/api/asset-categories').flush([], { status: 500, statusText: 'Error' });
    http.expectOne('/api/portfolios/1/category-targets');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Tentar novamente');
    expect(fixture.componentInstance.ready()).toBe(false);
  });
});
