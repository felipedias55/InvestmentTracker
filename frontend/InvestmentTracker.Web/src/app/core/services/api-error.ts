import { HttpErrorResponse } from '@angular/common/http';
export function apiError(error: HttpErrorResponse): string {
  if (error.status === 0)
    return 'Não foi possível conectar à API. Verifique a conexão e tente novamente.';
  if (error.status === 404) return 'O registro não está mais disponível. Atualize a lista.';
  if (error.error?.detail) return error.error.detail;
  if (error.error?.message) return error.error.message;
  if (error.error?.errors) return (Object.values(error.error.errors).flat() as string[]).join(' ');
  return 'Não foi possível concluir a operação. Tente novamente.';
}
