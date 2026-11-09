import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CurrentUser } from './access.model';
import { AUTH_IDLE_MS, AUTH_WARMUP_URL, Auth } from './auth';
import { AUTH_RETRY_HEADER, ntlmGateInterceptor } from './ntlm-gate.interceptor';

const user: CurrentUser = {
  name: 'SENSE\\tester',
  login: 'tester',
  domain: 'SENSE',
  fullName: 'Тестовий Казначей',
  authenticationType: 'NTLM',
  roles: ['TPReader'],
  permissions: ['TPDictionaryRead'],
  devAccess: false,
};

const badRequest = { status: 400, statusText: 'Bad Request' };
const markedBadRequest = { ...badRequest, headers: { [AUTH_RETRY_HEADER]: '1' } };

describe('ntlmGateInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(withInterceptors([ntlmGateInterceptor])), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
    vi.restoreAllMocks();
  });

  function warmUp(): void {
    TestBed.inject(Auth).ready().subscribe();
    backend.expectOne(AUTH_WARMUP_URL).flush(user);
  }

  it('держит залп до конца прогрева и прогревает один раз', () => {
    const got: string[] = [];
    http.get<string>('api/a').subscribe(value => got.push(value));
    http.get<string>('api/b').subscribe(value => got.push(value));

    backend.expectNone('api/a');
    backend.expectNone('api/b');
    backend.expectOne(AUTH_WARMUP_URL).flush(user);

    backend.expectOne('api/a').flush('a');
    backend.expectOne('api/b').flush('b');
    expect(got).toEqual(['a', 'b']);
    expect(TestBed.inject(Auth).user()).toEqual(user);
  });

  it('после сбоя handshake делает один общий перепрогрев и повторяет каждый запрос', () => {
    warmUp();
    const got: string[] = [];
    http.get<string>('api/a').subscribe(value => got.push(value));
    http.get<string>('api/b').subscribe(value => got.push(value));

    backend.expectOne('api/a').flush(null, badRequest);
    backend.expectOne('api/b').flush(null, badRequest);

    backend.expectOne(AUTH_WARMUP_URL).flush(user);
    backend.expectOne('api/a').flush('a');
    backend.expectOne('api/b').flush('b');
    expect(got).toEqual(['a', 'b']);
  });

  it('повторяет POST, только если сервер пометил 400 как сбой handshake', () => {
    warmUp();
    let status = 0;
    let body = '';
    http.post('api/plain', {}).subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });
    http.post<string>('api/marked', {}).subscribe(value => (body = value));

    backend.expectOne('api/plain').flush(null, badRequest);
    expect(status).toBe(400);

    backend.expectOne('api/marked').flush(null, markedBadRequest);
    backend.expectOne(AUTH_WARMUP_URL).flush(user);
    backend.expectOne('api/marked').flush('ok');
    expect(body).toBe('ok');
  });

  it('повторяет запрос не больше одного раза', () => {
    warmUp();
    let status = 0;
    http.get('api/a').subscribe({ error: (e: HttpErrorResponse) => (status = e.status) });

    backend.expectOne('api/a').flush(null, badRequest);
    backend.expectOne(AUTH_WARMUP_URL).flush(user);
    backend.expectOne('api/a').flush(null, badRequest);
    expect(status).toBe(400);
  });

  it('не повторяет 400, который пришёл из конвейера приложения', () => {
    warmUp();
    let status = 0;
    http.get('api/dictionaries/currency').subscribe({
      error: (e: HttpErrorResponse) => (status = e.status),
    });

    // ProblemDetails: ошибка процедуры, а не сбой handshake — ни перепрогрева, ни повтора
    backend
      .expectOne('api/dictionaries/currency')
      .flush({ title: 'Помилка процедури', status: 400 }, badRequest);

    expect(status).toBe(400);
    backend.expectNone(AUTH_WARMUP_URL);
  });

  it('не повторяет 401 и 403', () => {
    warmUp();
    const statuses: number[] = [];
    const collect = { error: (e: HttpErrorResponse) => statuses.push(e.status) };
    http.get('api/a').subscribe(collect);
    http.get('api/b').subscribe(collect);

    backend.expectOne('api/a').flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne('api/b').flush(null, { status: 403, statusText: 'Forbidden' });
    expect(statuses).toEqual([401, 403]);
  });

  it('повторяет упавший прогрев перед тем, как отправить POST', () => {
    let body = '';
    http.post<string>('api/save', {}).subscribe(value => (body = value));

    backend.expectOne(AUTH_WARMUP_URL).flush(null, badRequest);
    backend.expectNone('api/save');
    backend.expectOne(AUTH_WARMUP_URL).flush(user);
    backend.expectOne('api/save').flush('ok');
    expect(body).toBe('ok');
  });

  it('прогревает заново после простоя', () => {
    const now = vi.spyOn(Date, 'now').mockReturnValue(1_000_000);
    warmUp();
    http.get('api/a').subscribe();
    backend.expectOne('api/a').flush('a');

    now.mockReturnValue(1_000_000 + AUTH_IDLE_MS + 1);
    http.get('api/b').subscribe();
    backend.expectNone('api/b');
    backend.expectOne(AUTH_WARMUP_URL).flush(user);
    backend.expectOne('api/b').flush('b');
  });

  it('пропускает запросы мимо API без прогрева', () => {
    http.get('i18n/uk.json').subscribe();
    backend.expectNone(AUTH_WARMUP_URL);
    backend.expectOne('i18n/uk.json').flush({});
  });
});
