import { HttpInterceptorFn } from '@angular/common/http';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.includes('/api/')) {
    return next(request);
  }

  return next(request.clone({ withCredentials: true }));
};
